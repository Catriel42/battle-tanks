import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './register.component.html',
  styleUrl: './register.component.scss'
})
export class RegisterComponent {
  private authService = inject(AuthService);
  private router = inject(Router);

  username = '';
  email = '';
  password = '';
  isLoading = signal(false);
  errorMsg = signal<string | null>(null);
  successMsg = signal<string | null>(null);

  onSubmit() {
    if (!this.username || !this.email || !this.password) {
      this.errorMsg.set('All fields are required.');
      return;
    }

    this.isLoading.set(true);
    this.errorMsg.set(null);
    this.successMsg.set(null);

    this.authService.register(this.username, this.email, this.password).subscribe({
      next: () => {
        this.isLoading.set(false);
        this.successMsg.set('Registration successful! Redirecting...');
        setTimeout(() => {
          this.router.navigate(['/login']);
        }, 1500);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.errorMsg.set(err.error?.message || 'Username or email already exists.');
      }
    });
  }
}
