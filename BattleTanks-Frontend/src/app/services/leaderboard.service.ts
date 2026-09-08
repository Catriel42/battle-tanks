import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';

export interface LeaderboardEntry {
  rank: number;
  playerId: string;
  username: string;
  score: number;
  scoreType: string;
}

interface LeaderboardResponse {
  type: string;
  entries: LeaderboardEntry[];
}

@Injectable({
  providedIn: 'root'
})
export class LeaderboardService {
  private http = inject(HttpClient);
  private apiUrl = 'http://localhost:5000/api/leaderboard';

  topKills = signal<LeaderboardEntry[]>([]);
  loading = signal(false);
  error = signal<string | null>(null);

  loadLeaderboard(top: number = 10): void {
    this.loading.set(true);
    this.error.set(null);

    this.http.get<LeaderboardResponse>(`${this.apiUrl}/kills?top=${top}`).subscribe({
      next: (data) => {
        console.log('Leaderboard data:', data);
        this.topKills.set(data?.entries || []);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Leaderboard error:', err);
        this.error.set(err?.error?.message || err?.message || 'Failed to load leaderboard');
        this.topKills.set([]);
        this.loading.set(false);
      }
    });
  }
}
