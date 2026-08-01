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
import { PlayerPosition } from '../../models';
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
  position = signal<PlayerPosition>({ x: 40, y: 40 });
  private explosions: { x: number; y: number; radius: number; maxRadius: number }[] = [];

  constructor() {
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

    return true;
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
      newY -= this.speed; moved = true;
    }
    if (this.keysPressed.has('s') || this.keysPressed.has('arrowdown')) {
      newY += this.speed; moved = true;
    }
    if (this.keysPressed.has('a') || this.keysPressed.has('arrowleft')) {
      newX -= this.speed; moved = true;
    }
    if (this.keysPressed.has('d') || this.keysPressed.has('arrowright')) {
      newX += this.speed; moved = true;
    }

   
    if (moved) {
      if (this.canMoveTo(newX, newY)) {
        this.position.set({ x: newX, y: newY });
        this.gameService.sendPlayerMove({ x: newX, y: newY });
      } else if (this.canMoveTo(newX, y)) {
        this.position.set({ x: newX, y });
        this.gameService.sendPlayerMove({ x: newX, y });
      } else if (this.canMoveTo(x, newY)) {
        this.position.set({ x, y: newY });
        this.gameService.sendPlayerMove({ x, y: newY });
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

   
    const { x, y } = this.position();
    this.ctx.fillStyle = '#b00000';
    this.ctx.fillRect(x - this.cameraX, y - this.cameraY, this.tankSize, this.tankSize);

   
    this.playerStore.players().forEach((player) => {
      if (player.position) {
        this.ctx.fillStyle = '#008800';
        this.ctx.fillRect(player.position.x - this.cameraX, player.position.y - this.cameraY, this.tankSize, this.tankSize);
      }
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
