import {
  Component,
  ElementRef,
  viewChild,
  afterNextRender,
  inject,
  DestroyRef,
  signal,
  effect,
} from '@angular/core';
import { Subscription } from 'rxjs';
import { fromEvent } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Game } from '../../services/game';
import { PlayerPosition } from '../../models';
import { PlayerStore } from '../../store/players.store';

@Component({
  selector: 'app-game-canvas',
  imports: [],
  templateUrl: './game-canvas.html',
  styleUrl: './game-canvas.scss',
})
export class GameCanvas {
  canvasRef = viewChild.required<ElementRef<HTMLCanvasElement>>('canvasRef');

  private gameService: Game = inject(Game);
  private destroyRef = inject(DestroyRef);
  playerStore = inject(PlayerStore);

  private ctx!: CanvasRenderingContext2D;

  private readonly tankSize = 40;
  private readonly speed = 5;

  readonly canvasWidth = 800;
  readonly canvasHeight = 600;

  position = signal<PlayerPosition>({ x: 100, y: 100 });

  constructor() {
    fromEvent<KeyboardEvent>(window, 'keydown')
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => this.handleKeyDown(event));

    this.destroyRef.onDestroy(() => {
    });

    afterNextRender(() => {
      const canvas = this.canvasRef().nativeElement;
      const context = canvas.getContext('2d');
      if (context) {
        this.ctx = context;
        this.draw();
      }
    });

    effect(() => {
      this.position();
      this.playerStore.players();
      this.draw();
    });
  }

  private handleKeyDown(event: KeyboardEvent): void {
    const { x, y } = this.position();
    let newX = x;
    let newY = y;
    let moved = false;

    switch (event.key.toLowerCase()) {
      case 'w':
      case 'arrowup':
        if (y - this.speed >= 0) { newY -= this.speed; moved = true; }
        break;
      case 's':
      case 'arrowdown':
        if (y + this.tankSize + this.speed <= this.canvasHeight) { newY += this.speed; moved = true; }
        break;
      case 'a':
      case 'arrowleft':
        if (x - this.speed >= 0) { newX -= this.speed; moved = true; }
        break;
      case 'd':
      case 'arrowright':
        if (x + this.tankSize + this.speed <= this.canvasWidth) { newX += this.speed; moved = true; }
        break;
    }

    if (moved) {
      event.preventDefault();
      const newPos: PlayerPosition = { x: newX, y: newY };
      this.position.set(newPos);
      this.gameService.sendPlayerMove(newPos);
    }
  }

  private draw(): void {
    if (!this.ctx) return;

    const { x, y } = this.position();

    this.ctx.clearRect(0, 0, this.canvasWidth, this.canvasHeight);

    this.ctx.fillStyle = '#1e1e2e';
    this.ctx.fillRect(0, 0, this.canvasWidth, this.canvasHeight);

    this.ctx.fillStyle = '#00d4ff';
    this.ctx.fillRect(x, y, this.tankSize, this.tankSize);


    this.playerStore.players().forEach((player) => {
      if (player.position) {
        this.ctx.fillStyle = '#ff4757';
        this.ctx.fillRect(player.position.x, player.position.y, this.tankSize, this.tankSize);
      }
    });
  }
}
