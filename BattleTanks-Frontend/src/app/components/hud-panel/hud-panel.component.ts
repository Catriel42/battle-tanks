import { Component, inject, computed, signal, effect } from '@angular/core';
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
  
  localTank = computed(() => this.gameService.localTank());
  tanks = computed(() => this.gameService.tanks());
  
  fps = signal(0);
  private frameCount = 0;
  private lastTime = performance.now();
  
  sortedPlayers = computed(() => {
    return [...this.tanks()]
      .filter(t => !t.isEliminated)
      .sort((a, b) => {
        if (a.isAlive !== b.isAlive) return a.isAlive ? -1 : 1;
        return b.lives - a.lives;
      });
  });

  constructor() {
    let animationId: number;
    
    const countFrames = () => {
      this.frameCount++;
      const now = performance.now();
      const elapsed = now - this.lastTime;
      
      if (elapsed >= 1000) {
        this.fps.set(Math.round(this.frameCount));
        this.frameCount = 0;
        this.lastTime = now;
      }
      
      animationId = requestAnimationFrame(countFrames);
    };
    
    animationId = requestAnimationFrame(countFrames);
  }

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
