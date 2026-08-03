import {
  Component,
  ElementRef,
  viewChild,
  afterNextRender,
  inject,
  DestroyRef,
  signal,
  input,
} from '@angular/core';
import { fromEvent } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Game } from '../../services/game';
import { PlayerPosition, Direction, Bullet } from '../../models';

import { PlayerStore } from '../../store/players.store';
import { MapStore } from '../../store/map.store';
import mapData from '../../../assets/map.json';

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
  mapStore = inject(MapStore);

  private ctx!: CanvasRenderingContext2D;
  private animationFrameId = 0;
  private keysPressed = new Set<string>();

  private readonly tankSize = 40;
  private readonly speed = 5;
  private readonly tileSize = 40;

  readonly canvasWidth = 1200;
  readonly canvasHeight = 800;


  private readonly worldWidth = 30 * 40;
  private readonly worldHeight = 20 * 40;

  private cameraX = 0;
  private cameraY = 0;

  localUsername = input<string>('');
  position = signal<PlayerPosition>({ x: 40, y: 40, direction: 'UP' });
  private explosions: { x: number; y: number; radius: number; maxRadius: number }[] = [];
  private bullets: Bullet[] = [];
  private readonly bulletSpeed = 10;
  private lastShootTime = 0;
  private localTankImg!: HTMLImageElement;
  private enemyTankImg!: HTMLImageElement;
  private bulletImg!: HTMLImageElement;

  private loadImage(src: string): HTMLImageElement {
    const img = new Image();
    img.src = src;
    return img;
  }

  constructor() {
    this.localTankImg = this.loadImage('/assets/tank-green.png?v=2');
    this.enemyTankImg = this.loadImage('/assets/tank-red.png?v=2');
    this.bulletImg = this.loadImage('/assets/bullet.png?v=2');

    this.gameService.onShoot((payload) => {
      if (payload.id !== this.playerStore.localPlayerId()) {
        this.bullets.push({ x: payload.x, y: payload.y, direction: payload.direction as Direction, ownerId: payload.id });
      }
    });

    this.mapStore.loadMap(mapData as number[][]);

    this.gameService.onDestroyBlock((payload) => {
      this.mapStore.destroyBlock(payload.row, payload.col);
      this.addExplosion(payload.col * this.tileSize + 20, payload.row * this.tileSize + 20);
      if (payload.id && payload.id !== this.playerStore.localPlayerId()) {
        this.playerStore.incrementScore(payload.id, 10);
      }
    });

    this.gameService.onPlayerJoin(() => {
      // Announce our presence to the newly joined player
      this.gameService.sendPlayerMove(this.position());
    });

    fromEvent<KeyboardEvent>(window, 'keydown')
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        const key = event.key.toLowerCase();
        if (key === ' ') {
          event.preventDefault();
          this.shoot();
        } else if (['w', 'a', 's', 'd', 'arrowup', 'arrowdown', 'arrowleft', 'arrowright'].includes(key)) {
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


        fromEvent<MouseEvent>(canvas, 'mousedown')
          .pipe(takeUntilDestroyed(this.destroyRef))
          .subscribe((event) => {
            const rect = canvas.getBoundingClientRect();
            const mouseX = event.clientX - rect.left;
            const mouseY = event.clientY - rect.top;
            this.handleLocalClick(mouseX, mouseY);
          });

        this.gameLoop();

        // Announce our initial spawn position to everyone already in the game
        this.gameService.sendPlayerMove(this.position());
      }
    });
  }

  private handleLocalClick(screenX: number, screenY: number): void {
    const worldX = screenX + this.cameraX;
    const worldY = screenY + this.cameraY;

    const col = Math.floor(worldX / this.tileSize);
    const row = Math.floor(worldY / this.tileSize);

    const grid = this.mapStore.grid();
    if (grid[row] && grid[row][col] === 2) {
      this.mapStore.destroyBlock(row, col);
      const localId = this.playerStore.localPlayerId();
      this.gameService.sendDestroyBlock(row, col, localId ?? undefined);
      this.addExplosion(col * this.tileSize + 20, row * this.tileSize + 20);

      if (localId) {
        this.playerStore.incrementScore(localId, 10);
      }
    }
  }

  private addExplosion(worldX: number, worldY: number): void {
    this.explosions.push({ x: worldX, y: worldY, radius: 5, maxRadius: 30 });
  }

  private shoot(): void {
    const now = Date.now();
    if (now - this.lastShootTime < 500) return;

    const pos = this.position();
    const localId = this.playerStore.localPlayerId();
    if (!localId || !pos.direction) return;

    this.lastShootTime = now;
    let bx = pos.x;
    let by = pos.y;
    const bulletSize = 10;

    if (pos.direction === 'UP') {
      bx = pos.x + this.tankSize / 2 - bulletSize / 2;
      by = pos.y - bulletSize;
    } else if (pos.direction === 'DOWN') {
      bx = pos.x + this.tankSize / 2 - bulletSize / 2;
      by = pos.y + this.tankSize;
    } else if (pos.direction === 'LEFT') {
      bx = pos.x - bulletSize;
      by = pos.y + this.tankSize / 2 - bulletSize / 2;
    } else if (pos.direction === 'RIGHT') {
      bx = pos.x + this.tankSize;
      by = pos.y + this.tankSize / 2 - bulletSize / 2;
    }

    this.bullets.push({ x: bx, y: by, direction: pos.direction, ownerId: localId });
    this.gameService.sendShoot(localId, bx, by, pos.direction);
  }

  private canMoveTo(newX: number, newY: number): boolean {
    if (newX < 0 || newY < 0 || newX + this.tankSize > this.worldWidth || newY + this.tankSize > this.worldHeight) {
      return false;
    }

    const grid = this.mapStore.grid();
    const corners = [
      { x: newX, y: newY },
      { x: newX + this.tankSize - 1, y: newY },
      { x: newX, y: newY + this.tankSize - 1 },
      { x: newX + this.tankSize - 1, y: newY + this.tankSize - 1 }
    ];

    for (const corner of corners) {
      const col = Math.floor(corner.x / this.tileSize);
      const row = Math.floor(corner.y / this.tileSize);
      if (grid[row] && grid[row][col] > 0) {
        return false;
      }
    }

    const localId = this.playerStore.localPlayerId();
    for (const p of this.playerStore.players()) {
      if (p.id === localId || p.health <= 0 || !p.position) continue;
      const px = p.position.x;
      const py = p.position.y;
      if (newX < px + this.tankSize && newX + this.tankSize > px &&
          newY < py + this.tankSize && newY + this.tankSize > py) {
        return false;
      }
    }

    return true;
  }

  private gameLoop(): void {
    this.update();
    this.draw();
    this.animationFrameId = requestAnimationFrame(() => this.gameLoop());
  }

  private update(): void {
    const pos = this.position();
    const x = pos.x;
    const y = pos.y;
    let newX = x;
    let newY = y;
    let moved = false;
    let newDir = pos.direction || 'UP';

    if (this.keysPressed.has('w') || this.keysPressed.has('arrowup')) {
      newY -= this.speed; moved = true; newDir = 'UP';
    } else if (this.keysPressed.has('s') || this.keysPressed.has('arrowdown')) {
      newY += this.speed; moved = true; newDir = 'DOWN';
    } else if (this.keysPressed.has('a') || this.keysPressed.has('arrowleft')) {
      newX -= this.speed; moved = true; newDir = 'LEFT';
    } else if (this.keysPressed.has('d') || this.keysPressed.has('arrowright')) {
      newX += this.speed; moved = true; newDir = 'RIGHT';
    }

    if (moved || newDir !== pos.direction) {
       let finalX = x;
       let finalY = y;

       if (moved) {
          if (this.canMoveTo(newX, newY)) {
             finalX = newX; finalY = newY;
          } else if (this.canMoveTo(newX, y)) {
             finalX = newX;
          } else if (this.canMoveTo(x, newY)) {
             finalY = newY;
          }
       }

       this.position.set({ x: finalX, y: finalY, direction: newDir });
       this.gameService.sendPlayerMove({ x: finalX, y: finalY, direction: newDir });
    }

    for (let i = this.bullets.length - 1; i >= 0; i--) {
      const b = this.bullets[i];
      if (b.direction === 'UP') b.y -= this.bulletSpeed;
      if (b.direction === 'DOWN') b.y += this.bulletSpeed;
      if (b.direction === 'LEFT') b.x -= this.bulletSpeed;
      if (b.direction === 'RIGHT') b.x += this.bulletSpeed;

      if (b.x < 0 || b.y < 0 || b.x > this.worldWidth || b.y > this.worldHeight) {
        this.bullets.splice(i, 1);
        continue;
      }

      const col = Math.floor((b.x + 5) / this.tileSize);
      const row = Math.floor((b.y + 5) / this.tileSize);
      const grid = this.mapStore.grid();

      if (grid[row] && grid[row][col] > 0) {
        if (grid[row][col] === 2) {
          this.mapStore.destroyBlock(row, col);
          this.addExplosion(col * this.tileSize + 20, row * this.tileSize + 20);

          if (b.ownerId === this.playerStore.localPlayerId()) {
            this.playerStore.incrementScore(b.ownerId, 10);
            this.gameService.sendDestroyBlock(row, col, b.ownerId);
          }
        } else {
          this.addExplosion(b.x, b.y);
        }
        this.bullets.splice(i, 1);
        continue;
      }
    }

    const p = this.position();
    this.cameraX = Math.max(0, Math.min(p.x + this.tankSize / 2 - this.canvasWidth / 2, this.worldWidth - this.canvasWidth));
    this.cameraY = Math.max(0, Math.min(p.y + this.tankSize / 2 - this.canvasHeight / 2, this.worldHeight - this.canvasHeight));

    this.explosions = this.explosions.filter(exp => {
      exp.radius += 2;
      return exp.radius <= exp.maxRadius;
    });
  }

  private drawTank(x: number, y: number, direction: Direction | undefined, image: HTMLImageElement): void {
    this.ctx.save();
    this.ctx.translate(x - this.cameraX + this.tankSize / 2, y - this.cameraY + this.tankSize / 2);

    let angle = 0;
    if (direction === 'DOWN') angle = Math.PI;
    else if (direction === 'LEFT') angle = -Math.PI / 2;
    else if (direction === 'RIGHT') angle = Math.PI / 2;

    this.ctx.rotate(angle);
    this.ctx.drawImage(image, -this.tankSize / 2, -this.tankSize / 2, this.tankSize, this.tankSize);
    this.ctx.restore();
  }

  private draw(): void {
    if (!this.ctx) return;

    this.ctx.clearRect(0, 0, this.canvasWidth, this.canvasHeight);
    this.ctx.fillStyle = '#000000';
    this.ctx.fillRect(0, 0, this.canvasWidth, this.canvasHeight);

    const grid = this.mapStore.grid();
    for (let row = 0; row < grid.length; row++) {
      for (let col = 0; col < grid[row].length; col++) {
        const cell = grid[row][col];
        if (cell === 0) continue;

        const screenX = col * this.tileSize - this.cameraX;
        const screenY = row * this.tileSize - this.cameraY;

        if (screenX + this.tileSize < 0 || screenX > this.canvasWidth || screenY + this.tileSize < 0 || screenY > this.canvasHeight) {
          continue;
        }

        if (cell === 1) {
          this.ctx.fillStyle = '#b0b0b0';
        } else if (cell === 2) {
          this.ctx.fillStyle = '#cc5500';
        }
        this.ctx.fillRect(screenX, screenY, this.tileSize, this.tileSize);
      }
    }

    const { x, y, direction } = this.position();
    const localPlayer = this.playerStore.players().find(p => p.id === this.playerStore.localPlayerId());
    if (localPlayer && localPlayer.health > 0) {
      this.drawTank(x, y, direction, this.localTankImg);
    }

    this.playerStore.players().forEach((player) => {
      if (player.position && player.health > 0 && player.id !== this.playerStore.localPlayerId()) {
        this.drawTank(player.position.x, player.position.y, player.position.direction, this.enemyTankImg);
      }
    });

    this.bullets.forEach(b => {
      this.ctx.drawImage(this.bulletImg, b.x - this.cameraX, b.y - this.cameraY, 10, 10);
    });

    this.explosions.forEach(exp => {
      const screenX = exp.x - this.cameraX;
      const screenY = exp.y - this.cameraY;

      this.ctx.beginPath();
      this.ctx.arc(screenX, screenY, exp.radius, 0, Math.PI * 2);
      this.ctx.fillStyle = `rgba(255, 165, 0, ${1 - exp.radius / exp.maxRadius})`;
      this.ctx.fill();

      this.ctx.beginPath();
      this.ctx.arc(screenX, screenY, exp.radius * 0.6, 0, Math.PI * 2);
      this.ctx.fillStyle = `rgba(255, 255, 0, ${1 - exp.radius / exp.maxRadius})`;
      this.ctx.fill();
    });
  }
}
