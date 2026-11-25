import { queryOptions } from "@tanstack/react-query";
import { getMe } from "./identity.service";

export const identityKeys = {
  profile: () => ["profile"] as const,
  users: () => ["users"] as const,
  userList: () => [...identityKeys.users(), "list"] as const,
};

export const identityQueries = {
  profile: () => queryOptions({ queryKey: identityKeys.profile(), queryFn: () => getMe() }),
};
