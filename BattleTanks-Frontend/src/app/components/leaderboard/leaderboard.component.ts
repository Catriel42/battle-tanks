import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LeaderboardService } from '../../services/leaderboard.service';

@Component({
  selector: 'app-leaderboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './leaderboard.component.html',
  styleUrl: './leaderboard.component.scss'
})
export class LeaderboardComponent implements OnInit {
  private leaderboardService = inject(LeaderboardService);

  topCount = signal(10);
  topKills = this.leaderboardService.topKills;
  loading = this.leaderboardService.loading;
  error = this.leaderboardService.error;

  ngOnInit(): void {
    this.leaderboardService.loadLeaderboard(this.topCount());
  }
}
