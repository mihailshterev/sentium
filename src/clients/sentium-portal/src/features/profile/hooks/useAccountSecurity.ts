import { useMutation } from "@tanstack/react-query";
import { changePassword, signOutOtherSessions } from "@/features/auth/api/identity.service";

export function useAccountSecurity() {
  const passwordMutation = useMutation({
    mutationFn: (data: { currentPassword: string; newPassword: string }) => changePassword(data),
  });

  const sessionsMutation = useMutation({ mutationFn: () => signOutOtherSessions() });

  return { changePassword: passwordMutation, signOutOthers: sessionsMutation };
}
