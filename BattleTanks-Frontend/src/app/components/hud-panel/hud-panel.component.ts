import { Component, inject, computed } from '@angular/core';
import { GameService } from '../../services/game.service';

@Component({
  selector: 'app-hud-panel',
  standalone: true,
  imports: [],
  templateUrl: './hud-panel.component.html',
  styleUrl: './hud-panel.component.scss',
})
export class HudPanelComponent {
  private gameService = inject(GameService);
  
  // Computed values from game service
  localTank = computed(() => this.gameService.localTank());
  tanks = computed(() => this.gameService.tanks());
  currentTick = computed(() => this.gameService.currentTick());
  
  // Sorted players by kills (for leaderboard)
  sortedPlayers = computed(() => {
    return [...this.tanks()]
      .filter(t => !t.isEliminated)
      .sort((a, b) => {
        // Sort by alive status first, then by lives
        if (a.isAlive !== b.isAlive) return a.isAlive ? -1 : 1;
        return b.lives - a.lives;
      });
  });

  getHearts(health: number, maxHealth = 3): string[] {
    const hearts: string[] = [];
    for (let i = 0; i < maxHealth; i++) {
      hearts.push(i < health ? '❤️' : '🖤');
    }
    return hearts;
  }

  getLives(lives: number): string[] {
    const livesArray: string[] = [];
    for (let i = 0; i < lives; i++) {
      livesArray.push('🛡️');
    }
    return livesArray;
  }
}
