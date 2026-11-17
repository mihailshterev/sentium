import type { UploadRules } from "@/features/workspaces/types/workspace";
import { formatBytesToMb } from "@/shared/utils/formatters";

export const fileExtension = (name: string): string => {
  const dot = name.lastIndexOf(".");
  return dot < 0 ? "" : name.substring(dot).toLowerCase();
};

/**
 * Why the server would reject `file`, or null when it looks acceptable. Without rules (still loading) everything
 * passes and the server has the final say.
 */
export const uploadProblem = (file: File, rules: UploadRules | null): string | null => {
  if (file.size === 0) {
    return `"${file.name}" is empty.`;
  }
  if (!rules) {
    return null;
  }
  const ext = fileExtension(file.name);
  if (!rules.extensions.includes(ext)) {
    return `"${file.name}": ${ext ? `${ext} files are` : "files without an extension are"} not supported.`;
  }
  if (file.size > rules.maxFileSizeBytes) {
    return `"${file.name}" is larger than ${formatBytesToMb(rules.maxFileSizeBytes)}.`;
  }
  return null;
};

/** The accepted extensions as an `accept` attribute value for file inputs. */
export const acceptAttribute = (rules: UploadRules | null): string | undefined => rules?.extensions.join(",");
