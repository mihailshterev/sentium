import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  fetchHouseholdCalendar,
  fetchUserCalendar,
  updateHouseholdCalendar,
  updateUserCalendar,
  type HouseholdCalendar,
  type UserCalendar,
} from "@/features/registry/api/registry.service";

const USER_KEY = ["settings-calendar-user"] as const;
const HOUSEHOLD_KEY = ["settings-calendar"] as const;

export interface CalendarSave {
  own: UserCalendar;
  /** Only a Sovereign can change the household defaults. */
  household?: HouseholdCalendar;
}

/**
 * The user's own time zone and day-start, and (for a Sovereign) the household defaults they fall back to. Saving
 * either re-arms the unsent timeline Reminders server-side.
 */
export const useCalendarSettings = (isSovereign: boolean) => {
  const qc = useQueryClient();

  const own = useQuery({ queryKey: USER_KEY, queryFn: fetchUserCalendar, retry: false });
  const household = useQuery({
    queryKey: HOUSEHOLD_KEY,
    queryFn: fetchHouseholdCalendar,
    enabled: isSovereign,
    retry: false,
  });

  const {
    mutate: save,
    isPending: isSaving,
    isSuccess: isSaveSuccess,
    isError: isSaveError,
    error: saveError,
    reset: resetSave,
  } = useMutation({
    mutationFn: async ({ own: ownValue, household: householdValue }: CalendarSave) => {
      if (householdValue) {
        qc.setQueryData(HOUSEHOLD_KEY, await updateHouseholdCalendar(householdValue));
      }
      qc.setQueryData(USER_KEY, await updateUserCalendar(ownValue));
    },
  });

  return {
    own: own.data?.value ?? null,
    household: household.data?.value ?? null,
    isLoading: own.isLoading || (isSovereign && household.isLoading),
    save,
    isSaving,
    isSaveSuccess,
    isSaveError,
    saveError,
    resetSave,
  };
};
