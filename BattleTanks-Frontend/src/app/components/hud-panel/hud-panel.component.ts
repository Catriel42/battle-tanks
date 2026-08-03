import { Component, inject, input } from '@angular/core';
import { PlayerStore } from '../../store/players.store';

@Component({
  selector: 'app-hud-panel',
  standalone: true,
  imports: [],
  templateUrl: './hud-panel.component.html',
  styleUrl: './hud-panel.component.scss',
})
export class HudPanelComponent {
  playerStore = inject(PlayerStore);
  localUsername = input<string>('');

  getHearts(health: number): string[] {
    const hearts: string[] = [];
    for (let i = 0; i < 3; i++) {
      hearts.push(i < health ? '❤️' : '🖤');
    }
    return hearts;
  }
}
