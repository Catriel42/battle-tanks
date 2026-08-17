import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="glass-container">
      <div class="glass-card">
        <h1 class="title">BATTLE TANKS</h1>
        <h2 class="subtitle">Enlist as a new commander</h2>
        
        <form (ngSubmit)="onSubmit()">
          <div class="form-group">
            <label>USERNAME</label>
            <input type="text" [(ngModel)]="username" name="username" required autocomplete="off" />
          </div>

          <div class="form-group">
            <label>EMAIL</label>
            <input type="email" [(ngModel)]="email" name="email" required autocomplete="off" />
          </div>
          
          <div class="form-group">
            <label>PASSWORD</label>
            <input type="password" [(ngModel)]="password" name="password" required />
          </div>
          
          @if (errorMsg()) {
            <div class="error-box">
              {{ errorMsg() }}
            </div>
          }
          @if (successMsg()) {
            <div class="success-box">
              {{ successMsg() }}
            </div>
          }
          
          <button type="submit" class="btn-primary" [disabled]="isLoading()">
            @if (isLoading()) {
              ENLISTING...
            } @else {
              CREATE ACCOUNT
            }
          </button>
        </form>
        
        <div class="footer-links">
          <p>Already a commander? <a routerLink="/login">Login here</a></p>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .glass-container {
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      background: linear-gradient(45deg, #0f172a, #1e1b4b);
      font-family: 'Inter', sans-serif;
    }
    .glass-card {
      background: rgba(255, 255, 255, 0.03);
      backdrop-filter: blur(16px);
      -webkit-backdrop-filter: blur(16px);
      border: 1px solid rgba(255, 255, 255, 0.05);
      border-radius: 16px;
      padding: 40px;
      width: 100%;
      max-width: 400px;
      box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.5);
      display: flex;
      flex-direction: column;
      gap: 20px;
    }
    .title {
      color: #fff;
      font-size: 2rem;
      font-weight: 800;
      letter-spacing: 2px;
      text-align: center;
      margin: 0;
      background: -webkit-linear-gradient(#60a5fa, #c084fc);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
    }
    .subtitle {
      color: #94a3b8;
      font-size: 0.9rem;
      text-align: center;
      margin: 0 0 10px 0;
      font-weight: 400;
    }
    .form-group {
      display: flex;
      flex-direction: column;
      gap: 8px;
      margin-bottom: 20px;
    }
    label {
      color: #64748b;
      font-size: 0.75rem;
      font-weight: 600;
      letter-spacing: 1px;
    }
    input {
      background: rgba(0, 0, 0, 0.2);
      border: 1px solid rgba(255, 255, 255, 0.1);
      padding: 12px 16px;
      border-radius: 8px;
      color: #fff;
      font-size: 1rem;
      outline: none;
      transition: all 0.3s ease;
    }
    input:focus {
      border-color: #60a5fa;
      background: rgba(0, 0, 0, 0.4);
    }
    .btn-primary {
      background: linear-gradient(90deg, #3b82f6, #8b5cf6);
      color: white;
      border: none;
      padding: 14px;
      border-radius: 8px;
      font-size: 1rem;
      font-weight: 600;
      letter-spacing: 1px;
      cursor: pointer;
      transition: opacity 0.3s ease;
      width: 100%;
      margin-top: 10px;
    }
    .btn-primary:hover {
      opacity: 0.9;
    }
    .btn-primary:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }
    .footer-links {
      text-align: center;
      margin-top: 15px;
      color: #64748b;
      font-size: 0.85rem;
    }
    .footer-links a {
      color: #60a5fa;
      text-decoration: none;
      transition: color 0.3s ease;
    }
    .footer-links a:hover {
      color: #93c5fd;
    }
    .error-box {
      background: rgba(239, 68, 68, 0.1);
      border: 1px solid rgba(239, 68, 68, 0.3);
      color: #fca5a5;
      padding: 10px;
      border-radius: 8px;
      font-size: 0.85rem;
      text-align: center;
      margin-bottom: 10px;
    }
    .success-box {
      background: rgba(34, 197, 94, 0.1);
      border: 1px solid rgba(34, 197, 94, 0.3);
      color: #86efac;
      padding: 10px;
      border-radius: 8px;
      font-size: 0.85rem;
      text-align: center;
      margin-bottom: 10px;
    }
  `]
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
