import {
  Component,
  ElementRef,
  viewChild,
  afterNextRender,
  inject,
  DestroyRef,
  signal,
} from '@angular/core';
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
  private animationFrameId = 0;
  private keysPressed = new Set<string>();

  private readonly tankSize = 40;
  private readonly speed = 5;

  readonly canvasWidth = 800;
  readonly canvasHeight = 600;

  position = signal<PlayerPosition>({ x: 100, y: 100 });

  constructor() {
    fromEvent<KeyboardEvent>(window, 'keydown')
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        const key = event.key.toLowerCase();
        if (['w', 'a', 's', 'd', 'arrowup', 'arrowdown', 'arrowleft', 'arrowright'].includes(key)) {
          event.preventDefault();
          this.keysPressed.add(key);
        }
      });

    fromEvent<KeyboardEvent>(window, 'keyup')
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        this.keysPressed.delete(event.key.toLowerCase());
      });

    fromEvent(window, 'blur')
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.keysPressed.clear();
      });

    this.destroyRef.onDestroy(() => {
      cancelAnimationFrame(this.animationFrameId);
    });

    afterNextRender(() => {
      const canvas = this.canvasRef().nativeElement;
      const context = canvas.getContext('2d');
      if (context) {
        this.ctx = context;
        this.gameLoop();
      }
    });
  }

  private gameLoop(): void {
    this.update();
    this.draw();
    this.animationFrameId = requestAnimationFrame(() => this.gameLoop());
  }

  private update(): void {
    const { x, y } = this.position();
    let newX = x;
    let newY = y;
    let moved = false;

    if (this.keysPressed.has('w') || this.keysPressed.has('arrowup')) {
      if (y - this.speed >= 0) { newY -= this.speed; moved = true; }
    }
    if (this.keysPressed.has('s') || this.keysPressed.has('arrowdown')) {
      if (y + this.tankSize + this.speed <= this.canvasHeight) { newY += this.speed; moved = true; }
    }
    if (this.keysPressed.has('a') || this.keysPressed.has('arrowleft')) {
      if (x - this.speed >= 0) { newX -= this.speed; moved = true; }
    }
    if (this.keysPressed.has('d') || this.keysPressed.has('arrowright')) {
      if (x + this.tankSize + this.speed <= this.canvasWidth) { newX += this.speed; moved = true; }
    }

    if (moved) {
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
