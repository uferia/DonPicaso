import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';

export const DEVICE_BRANCH_ID_STORAGE_KEY = 'donpicaso.deviceBranchId';

@Component({
  selector: 'app-device-setup',
  imports: [FormsModule],
  templateUrl: './device-setup.html',
  styleUrl: './device-setup.scss',
})
export class DeviceSetup {
  private readonly router = inject(Router);

  protected branchId = '';
  protected readonly errorMessage = signal<string | null>(null);

  save(): void {
    if (!this.branchId.trim()) {
      this.errorMessage.set('Branch ID is required.');
      return;
    }

    localStorage.setItem(DEVICE_BRANCH_ID_STORAGE_KEY, this.branchId.trim());
    void this.router.navigateByUrl('/staff-login');
  }
}
