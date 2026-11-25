import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { settingsQueries } from "../api/registry.queries";
import { buildSections } from "../catalog/build-sections";

export function useSettingsSchema(isSovereign: boolean) {
  const { data, error, isLoading } = useQuery({ ...settingsQueries.schema(isSovereign), retry: false });

  const sections = useMemo(() => (data ? buildSections(data) : []), [data]);
  return { sections, error, isLoading };
}
