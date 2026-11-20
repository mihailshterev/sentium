using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Sentium.Tests.Unit.Modules;

/// <summary>
/// Finds the types each type of an assembly references, from its metadata and IL: base type, interfaces, generic
/// constraints, attributes, member signatures, locals, catch clauses and every type or member token in a method body.
/// </summary>
/// <remarks>
/// Names are in metadata form (<c>Namespace.Outer/Nested</c>, <c>List`1</c>), generic arguments count as references
/// of their own, and a dependency matches a referenced name it is a prefix of, so a namespace matches every type in it.
/// A nested type, including a compiler-generated closure, is a type of its own: its references are not its parent's.
/// </remarks>
internal static class TypeDependencyScanner
{
    private static readonly ConcurrentDictionary<Assembly, IReadOnlyDictionary<string, IReadOnlySet<string>>> Scans = new();

    private static readonly Dictionary<ushort, OperandType> OperandTypes = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(opCode => unchecked((ushort)opCode.Value), opCode => opCode.OperandType);

    public static string MetadataName(Type type)
    {
        return type.FullName!.Replace('+', '/');
    }

    /// <summary>
    /// The metadata names of the given types that reference any of the dependencies.
    /// </summary>
    public static IReadOnlyList<string> TypesDependingOnAny(Assembly assembly, IEnumerable<Type> types, IEnumerable<string> dependencies)
    {
        var references = Scan(assembly);
        var lookup = dependencies
            .Select(d => d.Replace('+', '/'))
            .ToHashSet(StringComparer.Ordinal)
            .GetAlternateLookup<ReadOnlySpan<char>>();

        return types
            .Select(MetadataName)
            .Where(name => references.TryGetValue(name, out var referenced) && referenced.Any(r => MatchesAny(r, lookup)))
            .ToList();
    }

    public static IReadOnlyDictionary<string, IReadOnlySet<string>> Scan(Assembly assembly)
    {
        return Scans.GetOrAdd(assembly, Read);
    }

    /// <summary>
    /// True when the referenced name is a dependency or lies inside one: a type in a dependency namespace, or a type
    /// nested in a dependency type. <c>Foo</c> does not match <c>FooBar</c>.
    /// </summary>
    private static bool MatchesAny(string referenced, HashSet<string>.AlternateLookup<ReadOnlySpan<char>> dependencies)
    {
        if (dependencies.Contains(referenced))
        {
            return true;
        }

        for (var i = 0; i < referenced.Length; i++)
        {
            if (referenced[i] is '.' or '/' && dependencies.Contains(referenced.AsSpan(0, i)))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyDictionary<string, IReadOnlySet<string>> Read(Assembly assembly)
    {
        using var stream = File.OpenRead(assembly.Location);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        var names = new NameProvider(reader);
        var own = new Dictionary<TypeDefinitionHandle, HashSet<string>>();

        foreach (var handle in reader.TypeDefinitions)
        {
            var name = names.DefinitionName(handle);
            if (name == "<Module>")
            {
                continue;
            }

            names.Sink = new HashSet<string>(StringComparer.Ordinal);
            new TypeVisitor(reader, pe, names).Visit(handle);
            own[handle] = names.Sink;
        }

        var result = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
        foreach (var (handle, references) in own)
        {
            var all = new HashSet<string>(references, StringComparer.Ordinal);
            AddNested(reader, own, handle, all);
            result[names.DefinitionName(handle)] = all;
        }

        return result;
    }

    private static void AddNested(
        MetadataReader reader,
        Dictionary<TypeDefinitionHandle, HashSet<string>> own,
        TypeDefinitionHandle handle,
        HashSet<string> into)
    {
        foreach (var nested in reader.GetTypeDefinition(handle).GetNestedTypes())
        {
            into.UnionWith(own[nested]);
            AddNested(reader, own, nested, into);
        }
    }

    private sealed class TypeVisitor(MetadataReader reader, PEReader pe, NameProvider names)
    {
        public void Visit(TypeDefinitionHandle handle)
        {
            var type = reader.GetTypeDefinition(handle);

            AddEntity(type.BaseType);
            foreach (var implementation in type.GetInterfaceImplementations())
            {
                AddEntity(reader.GetInterfaceImplementation(implementation).Interface);
            }

            AddGenericConstraints(type.GetGenericParameters());
            AddAttributes(type.GetCustomAttributes());

            foreach (var fieldHandle in type.GetFields())
            {
                var field = reader.GetFieldDefinition(fieldHandle);
                field.DecodeSignature(names, null);
                AddAttributes(field.GetCustomAttributes());
            }

            foreach (var propertyHandle in type.GetProperties())
            {
                var property = reader.GetPropertyDefinition(propertyHandle);
                property.DecodeSignature(names, null);
                AddAttributes(property.GetCustomAttributes());
            }

            foreach (var eventHandle in type.GetEvents())
            {
                var @event = reader.GetEventDefinition(eventHandle);
                AddEntity(@event.Type);
                AddAttributes(@event.GetCustomAttributes());
            }

            foreach (var methodHandle in type.GetMethods())
            {
                VisitMethod(reader.GetMethodDefinition(methodHandle));
            }
        }

        private void VisitMethod(MethodDefinition method)
        {
            method.DecodeSignature(names, null);
            AddGenericConstraints(method.GetGenericParameters());
            AddAttributes(method.GetCustomAttributes());

            foreach (var parameterHandle in method.GetParameters())
            {
                AddAttributes(reader.GetParameter(parameterHandle).GetCustomAttributes());
            }

            if (method.RelativeVirtualAddress == 0)
            {
                return;
            }

            var body = pe.GetMethodBody(method.RelativeVirtualAddress);
            if (!body.LocalSignature.IsNil)
            {
                reader.GetStandaloneSignature(body.LocalSignature).DecodeLocalSignature(names, null);
            }

            foreach (var region in body.ExceptionRegions)
            {
                AddEntity(region.CatchType);
            }

            VisitIl(body.GetILReader());
        }

        private void VisitIl(BlobReader il)
        {
            while (il.RemainingBytes > 0)
            {
                ushort code = il.ReadByte();
                if (code == 0xFE)
                {
                    code = (ushort)(0xFE00 | il.ReadByte());
                }

                switch (OperandTypes[code])
                {
                    case OperandType.InlineNone:
                        break;
                    case OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar:
                        il.Offset += 1;
                        break;
                    case OperandType.InlineVar:
                        il.Offset += 2;
                        break;
                    case OperandType.InlineBrTarget or OperandType.InlineI or OperandType.ShortInlineR or OperandType.InlineString:
                        il.Offset += 4;
                        break;
                    case OperandType.InlineI8 or OperandType.InlineR:
                        il.Offset += 8;
                        break;
                    case OperandType.InlineSwitch:
                        var targets = il.ReadInt32();
                        il.Offset += 4 * targets;
                        break;
                    default:
                        AddEntity(MetadataTokens.EntityHandle(il.ReadInt32()));
                        break;
                }
            }
        }

        private void AddGenericConstraints(GenericParameterHandleCollection parameters)
        {
            foreach (var parameterHandle in parameters)
            {
                foreach (var constraintHandle in reader.GetGenericParameter(parameterHandle).GetConstraints())
                {
                    AddEntity(reader.GetGenericParameterConstraint(constraintHandle).Type);
                }
            }
        }

        private void AddAttributes(CustomAttributeHandleCollection attributes)
        {
            foreach (var attributeHandle in attributes)
            {
                AddEntity(reader.GetCustomAttribute(attributeHandle).Constructor);
            }
        }

        private void AddEntity(EntityHandle handle)
        {
            if (handle.IsNil)
            {
                return;
            }

            switch (handle.Kind)
            {
                case HandleKind.TypeDefinition:
                    names.GetTypeFromDefinition(reader, (TypeDefinitionHandle)handle, 0);
                    break;
                case HandleKind.TypeReference:
                    names.GetTypeFromReference(reader, (TypeReferenceHandle)handle, 0);
                    break;
                case HandleKind.TypeSpecification:
                    reader.GetTypeSpecification((TypeSpecificationHandle)handle).DecodeSignature(names, null);
                    break;
                case HandleKind.MemberReference:
                    AddMemberReference(reader.GetMemberReference((MemberReferenceHandle)handle));
                    break;
                case HandleKind.MethodDefinition:
                    var method = reader.GetMethodDefinition((MethodDefinitionHandle)handle);
                    AddEntity(method.GetDeclaringType());
                    method.DecodeSignature(names, null);
                    break;
                case HandleKind.FieldDefinition:
                    var field = reader.GetFieldDefinition((FieldDefinitionHandle)handle);
                    AddEntity(field.GetDeclaringType());
                    field.DecodeSignature(names, null);
                    break;
                case HandleKind.MethodSpecification:
                    var specification = reader.GetMethodSpecification((MethodSpecificationHandle)handle);
                    AddEntity(specification.Method);
                    specification.DecodeSignature(names, null);
                    break;
                case HandleKind.StandaloneSignature:
                    var signature = reader.GetStandaloneSignature((StandaloneSignatureHandle)handle);
                    if (signature.GetKind() == StandaloneSignatureKind.Method)
                    {
                        signature.DecodeMethodSignature(names, null);
                    }

                    break;
            }
        }

        private void AddMemberReference(MemberReference member)
        {
            AddEntity(member.Parent);

            if (member.GetKind() == MemberReferenceKind.Method)
            {
                member.DecodeMethodSignature(names, null);
            }
            else
            {
                member.DecodeFieldSignature(names, null);
            }
        }
    }

    /// <summary>
    /// Decodes signatures into type names and records every named type it meets in <see cref="Sink"/>.
    /// </summary>
    private sealed class NameProvider(MetadataReader reader) : ISignatureTypeProvider<string, object?>
    {
        public HashSet<string> Sink { get; set; } = [];

        public string DefinitionName(TypeDefinitionHandle handle)
        {
            var type = reader.GetTypeDefinition(handle);
            var name = reader.GetString(type.Name);
            var declaring = type.GetDeclaringType();

            if (!declaring.IsNil)
            {
                return $"{DefinitionName(declaring)}/{name}";
            }

            var ns = reader.GetString(type.Namespace);
            return ns.Length == 0 ? name : $"{ns}.{name}";
        }

        public string GetTypeFromDefinition(MetadataReader metadata, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            return Record(DefinitionName(handle));
        }

        public string GetTypeFromReference(MetadataReader metadata, TypeReferenceHandle handle, byte rawTypeKind)
        {
            return Record(ReferenceName(handle));
        }

        public string GetTypeFromSpecification(MetadataReader metadata, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
        {
            return reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
        }

        public string GetPrimitiveType(PrimitiveTypeCode typeCode)
        {
            return Record($"System.{typeCode}");
        }

        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
        {
            return genericType;
        }

        public string GetSZArrayType(string elementType) => elementType;

        public string GetArrayType(string elementType, ArrayShape shape) => elementType;

        public string GetByReferenceType(string elementType) => elementType;

        public string GetPointerType(string elementType) => elementType;

        public string GetPinnedType(string elementType) => elementType;

        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;

        public string GetFunctionPointerType(MethodSignature<string> signature) => "method";

        public string GetGenericMethodParameter(object? genericContext, int index) => $"!!{index}";

        public string GetGenericTypeParameter(object? genericContext, int index) => $"!{index}";

        private string ReferenceName(TypeReferenceHandle handle)
        {
            var type = reader.GetTypeReference(handle);
            var name = reader.GetString(type.Name);

            if (type.ResolutionScope.Kind == HandleKind.TypeReference)
            {
                return $"{ReferenceName((TypeReferenceHandle)type.ResolutionScope)}/{name}";
            }

            var ns = reader.GetString(type.Namespace);
            return ns.Length == 0 ? name : $"{ns}.{name}";
        }

        private string Record(string name)
        {
            Sink.Add(name);
            return name;
        }
    }
}
