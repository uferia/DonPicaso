import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';

import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { PasswordModule } from 'primeng/password';

import { AuthService } from '../../../core/auth/auth.service';

@Component({
  selector: 'app-login',
  imports: [FormsModule, ButtonModule, InputTextModule, MessageModule, PasswordModule],
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class Login {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  protected email = '';
  protected password = '';
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly isSubmitting = signal(false);

  async submit(): Promise<void> {
    this.errorMessage.set(null);
    this.isSubmitting.set(true);

    try {
      await this.authService.login({ email: this.email, password: this.password });
      await this.router.navigateByUrl('/admin');
    } catch {
      this.errorMessage.set('Invalid email or password.');
    } finally {
      this.isSubmitting.set(false);
    }
  }
}
