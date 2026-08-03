import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { GameCanvas } from '../game-canvas/game-canvas';
import { HudPanelComponent } from '../hud-panel/hud-panel.component';
import { PlayerStore } from '../../store/players.store';

@Component({
  selector: 'app-game',
  standalone: true,
  imports: [GameCanvas, HudPanelComponent],
  templateUrl: './game.component.html',
  styleUrl: './game.component.scss',
})
export class GameComponent {
  private route = inject(ActivatedRoute);
  playerStore = inject(PlayerStore);

  localUsername = signal('');

  constructor() {
    this.route.queryParams.subscribe((params) => {
      this.localUsername.set(params['username'] ?? '');
    });
  }
}
