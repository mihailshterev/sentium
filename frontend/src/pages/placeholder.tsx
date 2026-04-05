interface PlaceholderProps {
  title: string;
}

const Placeholder = ({ title }: PlaceholderProps) => {
  return (
    <div className="p-8">
      <h1 className="text-2xl font-bold">{title} Page</h1>
      <p className="text-gray-500">
        This section is currently under construction.
      </p>
    </div>
  );
};

export default Placeholder;
