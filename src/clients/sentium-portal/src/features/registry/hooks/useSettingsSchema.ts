import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { fetchSettingsSchema } from "../api/registry.service";
import { buildSections } from "../catalog/build-sections";

/**
 * The schema only changes with a server release, so it is fetched once per role: the server lists household sections
 * only to a Sovereign.
 */
export function useSettingsSchema(isSovereign: boolean) {
  const { data, error, isLoading } = useQuery({
    queryKey: ["registry-settings-schema", isSovereign],
    queryFn: fetchSettingsSchema,
    staleTime: Infinity,
    retry: false,
  });

  const sections = useMemo(() => (data ? buildSections(data) : []), [data]);
  return { sections, error, isLoading };
}
