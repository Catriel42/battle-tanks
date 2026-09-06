import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-login',
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent {
  private authService = inject(AuthService);
  private router = inject(Router);

  username = '';
  password = '';
  isLoading = signal(false);
  errorMsg = signal<string | null>(null);

  onSubmit() {
    if (!this.username || !this.password) {
      this.errorMsg.set('Credentials are required.');
      return;
    }

    this.isLoading.set(true);
    this.errorMsg.set(null);

    this.authService.login(this.username, this.password).subscribe({
      next: () => {
        this.isLoading.set(false);
        this.router.navigate(['/room']);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.errorMsg.set('Invalid credentials or server error.');
      }
    });
  }
}
