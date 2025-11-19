export type UserRole = "Master" | "Nutritionist" | "Client";

export interface UserSummary {
  id: string;
  email: string;
  role: UserRole;
  parentUserId: string | null;
  trainingPeaksIcsUrl?: string | null;
}
