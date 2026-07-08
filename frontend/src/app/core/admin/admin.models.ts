import { Role } from '../auth/auth.models';

export interface Brand {
  id: string;
  name: string;
  isActive: boolean;
  createdAtUtc: string;
}

export interface Branch {
  id: string;
  brandId: string;
  name: string;
  isActive: boolean;
  createdAtUtc: string;
}

export interface AdminUser {
  id: string;
  email: string | null;
  displayName: string;
  role: Role;
  brandId: string | null;
  branchId: string | null;
  isActive: boolean;
  createdAtUtc: string;
}

export interface CreateUserRequest {
  email: string | null;
  displayName: string;
  role: Role;
  brandId: string | null;
  branchId: string | null;
  password: string | null;
  pin: string | null;
}

export interface UpdateUserRequest {
  displayName: string;
  role: Role;
  brandId: string | null;
  branchId: string | null;
  email: string | null;
  newPassword: string | null;
  newPin: string | null;
}

export interface ResetCredentialRequest {
  newPassword: string | null;
  newPin: string | null;
}
