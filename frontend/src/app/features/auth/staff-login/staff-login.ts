import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';

import { StaffRosterMember } from '../../../core/auth/auth.models';
import { AuthService } from '../../../core/auth/auth.service';
import { DEVICE_BRANCH_ID_STORAGE_KEY } from '../device-setup/device-setup';

@Component({
  selector: 'app-staff-login',
  templateUrl: './staff-login.html',
  styleUrl: './staff-login.scss',
  imports: [ButtonModule, MessageModule],
})
export class StaffLogin implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly digits = ['1', '2', '3', '4', '5', '6', '7', '8', '9', '0'];
  protected readonly roster = signal<StaffRosterMember[]>([]);
  protected readonly selectedMember = signal<StaffRosterMember | null>(null);
  protected readonly pin = signal('');
  protected readonly errorMessage = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    const branchId = localStorage.getItem(DEVICE_BRANCH_ID_STORAGE_KEY);
    if (!branchId) {
      void this.router.navigateByUrl('/device-setup');
      return;
    }

    this.roster.set(
      await firstValueFrom(this.http.get<StaffRosterMember[]>(`/api/v1/auth/staff/${branchId}/users`)),
    );
  }

  selectMember(member: StaffRosterMember): void {
    this.selectedMember.set(member);
    this.pin.set('');
    this.errorMessage.set(null);
  }

  pressDigit(digit: string): void {
    if (this.pin().length < 4) {
      this.pin.set(this.pin() + digit);
    }
  }

  async submitPin(): Promise<void> {
    const branchId = localStorage.getItem(DEVICE_BRANCH_ID_STORAGE_KEY);
    const member = this.selectedMember();
    if (!branchId || !member) {
      return;
    }

    try {
      await this.authService.staffLogin({ branchId, userId: member.userId, pin: this.pin() });
    } catch {
      this.errorMessage.set('Incorrect PIN.');
      this.pin.set('');
      return;
    }

    // Login succeeded - a navigation failure here isn't a PIN error, so it's
    // handled separately (fire-and-forget, matching DeviceSetup.save() and
    // StaffLogin.ngOnInit()'s redirect) rather than reusing the "Incorrect
    // PIN." message.
    void this.router.navigateByUrl('/pos');
  }
}
