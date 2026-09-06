import { Component, inject, signal, computed, OnInit, OnDestroy, effect } from '@angular/core';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { GameCanvas } from '../game-canvas/game-canvas';
import { HudPanelComponent } from '../hud-panel/hud-panel.component';
import { GameService } from '../../services/game.service';
import { GameOverEvent, PlayerStatsDto } from '../../models';

@Component({
  selector: 'app-game',
  imports: [GameCanvas, HudPanelComponent],
  templateUrl: './game.component.html',
  styleUrl: './game.component.scss',
})
export class GameComponent implements OnInit, OnDestroy {
  private router = inject(Router);
  private gameService = inject(GameService);

  private subscriptions: Subscription[] = [];

  // Game state
  localUsername = computed(() => this.gameService.username() ?? 'Unknown');
  roomStatus = computed(() => this.gameService.roomStatus());
  isConnected = computed(() => this.gameService.isConnected());
  localTank = computed(() => this.gameService.localTank());

  // Game over state
  showGameOver = signal(false);
  gameOverData = signal<GameOverEvent | null>(null);

  constructor() {
    // Redirect to lobby if not connected or not in a game
    effect(() => {
      if (!this.isConnected()) {
        console.log('[Game] Not connected, redirecting to lobby');
        this.router.navigate(['/lobby']);
      }
    });
  }

  ngOnInit(): void {
    // Check if we're actually in a game
    if (this.roomStatus() !== 'playing' && this.roomStatus() !== 'starting') {
      console.log('[Game] Not in a game, redirecting to lobby');
      this.router.navigate(['/lobby']);
      return;
    }

    // Subscribe to game over event
    this.subscriptions.push(
      this.gameService.onGameOver$.subscribe((event) => {
        this.gameOverData.set(event);
        this.showGameOver.set(true);
      })
    );
  }

  ngOnDestroy(): void {
    this.subscriptions.forEach(sub => sub.unsubscribe());
  }

  returnToLobby(): void {
    this.gameService.leaveRoom();
    this.router.navigate(['/lobby']);
  }

  // Helper for template
  formatTime(ms: number): string {
    const seconds = Math.floor(ms / 1000);
    const minutes = Math.floor(seconds / 60);
    const remainingSeconds = seconds % 60;
    return `${minutes}:${remainingSeconds.toString().padStart(2, '0')}`;
  }
}
