export enum Role {
  Corporate = 'Corporate',
  BrandOwner = 'BrandOwner',
  BranchManager = 'BranchManager',
  Staff = 'Staff',
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface StaffLoginRequest {
  branchId: string;
  userId: string;
  pin: string;
}

export interface AuthTokens {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
}

export interface StaffRosterMember {
  userId: string;
  displayName: string;
}
