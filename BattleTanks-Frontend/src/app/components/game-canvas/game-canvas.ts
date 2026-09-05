import {
  Component,
  ElementRef,
  viewChild,
  afterNextRender,
  inject,
  DestroyRef,
  signal,
  effect,
  computed,
} from '@angular/core';
import { fromEvent } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { GameService } from '../../services/game.service';
import { Direction, TankDto, BulletDto } from '../../models';

@Component({
  selector: 'app-game-canvas',
  imports: [],
  templateUrl: './game-canvas.html',
  styleUrl: './game-canvas.scss',
})
export class GameCanvas {
  canvasRef = viewChild.required<ElementRef<HTMLCanvasElement>>('canvasRef');

  private gameService = inject(GameService);
  private destroyRef = inject(DestroyRef);

  private ctx!: CanvasRenderingContext2D;
  private animationFrameId = 0;
  
  // Track which movement key is currently held
  private currentMoveDirection: Direction | null = null;
  private keysPressed = new Set<string>();

  // Constants
  private readonly tankSize = 40;
  private readonly tileSize = 40;
  private readonly bulletSize = 10;
  
  readonly canvasWidth = 1200;
  readonly canvasHeight = 800;

  // Camera position
  private cameraX = 0;
  private cameraY = 0;

  // Visual effects (client-side only for feedback)
  private explosions: { x: number; y: number; radius: number; maxRadius: number }[] = [];

  // Loaded images
  private localTankImg!: HTMLImageElement;
  private enemyTankImg!: HTMLImageElement;
  private bulletImg!: HTMLImageElement;
  private imagesLoaded = signal(false);

  // Computed values from game service
  private worldWidth = computed(() => this.gameService.mapWidth() * this.tileSize);
  private worldHeight = computed(() => this.gameService.mapHeight() * this.tileSize);

  constructor() {
    // Load images
    this.loadImages();

    // Subscribe to game events for visual effects
    this.gameService.onBlockDestroyed$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        this.addExplosion(
          event.col * this.tileSize + this.tileSize / 2,
          event.row * this.tileSize + this.tileSize / 2
        );
      });

    this.gameService.onPlayerHit$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        // Find victim position and add small explosion
        const victim = this.gameService.tanks().find(t => t.id === event.victimId);
        if (victim) {
          this.addExplosion(victim.x + this.tankSize / 2, victim.y + this.tankSize / 2, 20);
        }
      });

    this.gameService.onPlayerKilled$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        // Find victim position and add big explosion
        const victim = this.gameService.tanks().find(t => t.id === event.victimId);
        if (victim) {
          this.addExplosion(victim.x + this.tankSize / 2, victim.y + this.tankSize / 2, 50);
        }
      });

    // Keyboard input handling
    fromEvent<KeyboardEvent>(window, 'keydown')
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => this.handleKeyDown(event));

    fromEvent<KeyboardEvent>(window, 'keyup')
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => this.handleKeyUp(event));

    fromEvent(window, 'blur')
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.handleBlur());

    this.destroyRef.onDestroy(() => {
      cancelAnimationFrame(this.animationFrameId);
    });

    afterNextRender(() => {
      const canvas = this.canvasRef().nativeElement;
      const context = canvas.getContext('2d');
      if (context) {
        this.ctx = context;
        this.renderLoop();
      }
    });
  }

  private loadImages(): void {
    let loadedCount = 0;
    const totalImages = 3;

    const onLoad = () => {
      loadedCount++;
      if (loadedCount === totalImages) {
        this.imagesLoaded.set(true);
      }
    };

    this.localTankImg = new Image();
    this.localTankImg.onload = onLoad;
    this.localTankImg.src = '/assets/tank-green.png';

    this.enemyTankImg = new Image();
    this.enemyTankImg.onload = onLoad;
    this.enemyTankImg.src = '/assets/tank-red.png';

    this.bulletImg = new Image();
    this.bulletImg.onload = onLoad;
    this.bulletImg.src = '/assets/bullet.png';
  }

  private handleKeyDown(event: KeyboardEvent): void {
    // Only handle input if game is playing
    if (this.gameService.roomStatus() !== 'playing') return;
    
    const key = event.key.toLowerCase();
    
    // Shoot on space
    if (key === ' ' || key === 'spacebar') {
      event.preventDefault();
      this.gameService.shoot();
      return;
    }
    
    // Movement keys
    const directionMap: Record<string, Direction> = {
      'w': 'up',
      'arrowup': 'up',
      's': 'down',
      'arrowdown': 'down',
      'a': 'left',
      'arrowleft': 'left',
      'd': 'right',
      'arrowright': 'right',
    };
    
    const direction = directionMap[key];
    if (direction) {
      event.preventDefault();
      
      // Only send move_start if this is a new key press or direction change
      if (!this.keysPressed.has(key)) {
        this.keysPressed.add(key);
        
        // If we're already moving in a different direction, 
        // update to the new direction
        if (this.currentMoveDirection !== direction) {
          this.currentMoveDirection = direction;
          this.gameService.moveStart(direction);
        }
      }
    }
  }

  private handleKeyUp(event: KeyboardEvent): void {
    const key = event.key.toLowerCase();
    this.keysPressed.delete(key);
    
    // Check if the released key corresponds to current movement direction
    const directionMap: Record<string, Direction> = {
      'w': 'up',
      'arrowup': 'up',
      's': 'down',
      'arrowdown': 'down',
      'a': 'left',
      'arrowleft': 'left',
      'd': 'right',
      'arrowright': 'right',
    };
    
    const releasedDirection = directionMap[key];
    
    if (releasedDirection === this.currentMoveDirection) {
      // Check if another movement key is still pressed
      const stillPressedDirection = this.getActiveDirection();
      
      if (stillPressedDirection) {
        // Switch to the other pressed direction
        this.currentMoveDirection = stillPressedDirection;
        this.gameService.moveStart(stillPressedDirection);
      } else {
        // No more movement keys pressed, stop
        this.currentMoveDirection = null;
        this.gameService.moveStop();
      }
    }
  }

  private getActiveDirection(): Direction | null {
    const directionKeys: [string[], Direction][] = [
      [['w', 'arrowup'], 'up'],
      [['s', 'arrowdown'], 'down'],
      [['a', 'arrowleft'], 'left'],
      [['d', 'arrowright'], 'right'],
    ];
    
    for (const [keys, direction] of directionKeys) {
      if (keys.some(k => this.keysPressed.has(k))) {
        return direction;
      }
    }
    return null;
  }

  private handleBlur(): void {
    // Stop movement when window loses focus
    if (this.currentMoveDirection) {
      this.currentMoveDirection = null;
      this.keysPressed.clear();
      this.gameService.moveStop();
    }
  }

  private addExplosion(worldX: number, worldY: number, maxRadius = 30): void {
    this.explosions.push({ x: worldX, y: worldY, radius: 5, maxRadius });
  }

  private renderLoop(): void {
    this.update();
    this.draw();
    this.animationFrameId = requestAnimationFrame(() => this.renderLoop());
  }

  private update(): void {
    // Update camera to follow local tank
    const localTank = this.gameService.localTank();
    if (localTank) {
      const targetCameraX = localTank.x + this.tankSize / 2 - this.canvasWidth / 2;
      const targetCameraY = localTank.y + this.tankSize / 2 - this.canvasHeight / 2;
      
      // Clamp camera to world bounds
      this.cameraX = Math.max(0, Math.min(targetCameraX, this.worldWidth() - this.canvasWidth));
      this.cameraY = Math.max(0, Math.min(targetCameraY, this.worldHeight() - this.canvasHeight));
    }
    
    // Update explosions
    this.explosions = this.explosions.filter(exp => {
      exp.radius += 2;
      return exp.radius <= exp.maxRadius;
    });
  }

  private draw(): void {
    if (!this.ctx) return;

    const status = this.gameService.roomStatus();
    
    // Clear canvas
    this.ctx.fillStyle = '#000000';
    this.ctx.fillRect(0, 0, this.canvasWidth, this.canvasHeight);

    if (status === 'waiting') {
      this.drawWaitingScreen();
      return;
    }

    if (status === 'starting') {
      this.drawStartingScreen();
      return;
    }

    if (status === 'finished') {
      this.drawFinishedScreen();
      return;
    }
    // Draw map
    this.drawMap();
    
    // Draw tanks
    this.drawTanks();
    
    // Draw power-ups
    this.drawPowerUps();
    
    // Draw bullets
    this.drawBullets();
    
    // Draw explosions
    this.drawExplosions();
    
    // Draw HUD
    this.drawHUD();
  }

  private drawWaitingScreen(): void {
    this.ctx.fillStyle = '#333';
    this.ctx.fillRect(0, 0, this.canvasWidth, this.canvasHeight);
    
    this.ctx.fillStyle = '#fff';
    this.ctx.font = '32px Arial';
    this.ctx.textAlign = 'center';
    this.ctx.fillText('Waiting for players...', this.canvasWidth / 2, this.canvasHeight / 2);
  }

  private drawStartingScreen(): void {
    // Draw the map in background
    this.drawMap();
    this.drawTanks();
    
    // Draw countdown overlay
    this.ctx.fillStyle = 'rgba(0, 0, 0, 0.5)';
    this.ctx.fillRect(0, 0, this.canvasWidth, this.canvasHeight);
    
    this.ctx.fillStyle = '#fff';
    this.ctx.font = 'bold 72px Arial';
    this.ctx.textAlign = 'center';
    this.ctx.fillText('GET READY!', this.canvasWidth / 2, this.canvasHeight / 2);
  }

  private drawFinishedScreen(): void {
    // Keep last game state visible
    this.drawMap();
    this.drawTanks();
    
    // Draw game over overlay
    this.ctx.fillStyle = 'rgba(0, 0, 0, 0.7)';
    this.ctx.fillRect(0, 0, this.canvasWidth, this.canvasHeight);
    
    this.ctx.fillStyle = '#fff';
    this.ctx.font = 'bold 48px Arial';
    this.ctx.textAlign = 'center';
    this.ctx.fillText('GAME OVER', this.canvasWidth / 2, this.canvasHeight / 2);
  }

  private drawMap(): void {
    const grid = this.gameService.mapGrid();
    if (!grid.length) return;
    
    for (let row = 0; row < grid.length; row++) {
      for (let col = 0; col < grid[row].length; col++) {
        const screenX = col * this.tileSize - this.cameraX;
        const screenY = row * this.tileSize - this.cameraY;
        
        // Skip tiles outside viewport
        if (screenX + this.tileSize < 0 || screenX > this.canvasWidth ||
            screenY + this.tileSize < 0 || screenY > this.canvasHeight) {
          continue;
        }
        
        // Get effective tile (considering destroyed blocks)
        const tile = this.gameService.getTileAt(row, col);
        
        if (tile === 0) continue; // Empty
        
        if (tile === 1) {
          // Steel wall (indestructible)
          this.ctx.fillStyle = '#b0b0b0';
        } else if (tile === 2) {
          // Brick wall (destructible)
          this.ctx.fillStyle = '#cc5500';
        }
        
        this.ctx.fillRect(screenX, screenY, this.tileSize, this.tileSize);
        
        // Add border for visual clarity
        this.ctx.strokeStyle = '#000';
        this.ctx.lineWidth = 1;
        this.ctx.strokeRect(screenX, screenY, this.tileSize, this.tileSize);
      }
    }
  }

  private drawTanks(): void {
    if (!this.imagesLoaded()) return;
    
    const localId = this.gameService.connectionId();
    const tanks = this.gameService.tanks();
    
    for (const tank of tanks) {
      if (!tank.isAlive) continue;
      
      const isLocal = tank.id === localId;
      const image = isLocal ? this.localTankImg : this.enemyTankImg;
      
      this.drawTank(tank, image);
      
      // Draw health bar
      this.drawHealthBar(tank);
      
      // Draw username
      this.drawUsername(tank);
    }
  }

  private drawTank(tank: TankDto, image: HTMLImageElement): void {
    const screenX = tank.x - this.cameraX;
    const screenY = tank.y - this.cameraY;
    
    this.ctx.save();
    this.ctx.translate(screenX + this.tankSize / 2, screenY + this.tankSize / 2);
    
    // Rotate based on direction
    let angle = 0;
    switch (tank.direction) {
      case 'down': angle = Math.PI; break;
      case 'left': angle = -Math.PI / 2; break;
      case 'right': angle = Math.PI / 2; break;
    }
    
    this.ctx.rotate(angle);
    this.ctx.drawImage(image, -this.tankSize / 2, -this.tankSize / 2, this.tankSize, this.tankSize);
    this.ctx.restore();
  }

  private drawHealthBar(tank: TankDto): void {
    const screenX = tank.x - this.cameraX;
    const screenY = tank.y - this.cameraY - 10;
    const width = this.tankSize;
    const height = 5;
    
    // Background
    this.ctx.fillStyle = '#333';
    this.ctx.fillRect(screenX, screenY, width, height);
    
    // Health
    const healthPercent = tank.health / 3; // Assuming max health is 3
    this.ctx.fillStyle = healthPercent > 0.5 ? '#0f0' : healthPercent > 0.25 ? '#ff0' : '#f00';
    this.ctx.fillRect(screenX, screenY, width * healthPercent, height);
  }

  private drawUsername(tank: TankDto): void {
    const screenX = tank.x - this.cameraX + this.tankSize / 2;
    const screenY = tank.y - this.cameraY - 18;
    
    this.ctx.fillStyle = '#fff';
    this.ctx.font = '12px Arial';
    this.ctx.textAlign = 'center';
    this.ctx.fillText(tank.username, screenX, screenY);
  }

  private drawBullets(): void {
    const bullets = this.gameService.bullets();
    
    for (const bullet of bullets) {
      const screenX = bullet.x - this.cameraX;
      const screenY = bullet.y - this.cameraY;
      
      if (this.bulletImg.complete) {
        this.ctx.drawImage(this.bulletImg, screenX, screenY, this.bulletSize, this.bulletSize);
      } else {
        this.ctx.fillStyle = '#ff0';
        this.ctx.beginPath();
        this.ctx.arc(screenX + this.bulletSize / 2, screenY + this.bulletSize / 2, this.bulletSize / 2, 0, Math.PI * 2);
        this.ctx.fill();
      }
    }
  }

  private drawPowerUps(): void {
    const powerUps = this.gameService.powerUps();
    const powerUpSize = 30;
    
    powerUps.forEach((powerUp) => {
      if (powerUp.isCollected) return;
      
      const screenX = powerUp.x - this.cameraX;
      const screenY = powerUp.y - this.cameraY;
      
      this.ctx.fillStyle = '#b00000';
      this.ctx.fillRect(screenX, screenY, powerUpSize, powerUpSize);
      
      this.ctx.strokeStyle = '#ffffff';
      this.ctx.lineWidth = 2;
      this.ctx.strokeRect(screenX, screenY, powerUpSize, powerUpSize);
      
      this.ctx.fillStyle = '#ffffff';
      this.ctx.font = 'bold 16px Arial';
      this.ctx.textAlign = 'center';
      this.ctx.textBaseline = 'middle';
      this.ctx.fillText('♥', screenX + powerUpSize / 2, screenY + powerUpSize / 2);
    });
  }

  private drawExplosions(): void {
    for (const exp of this.explosions) {
      const screenX = exp.x - this.cameraX;
      const screenY = exp.y - this.cameraY;
      
      // Outer explosion
      this.ctx.beginPath();
      this.ctx.arc(screenX, screenY, exp.radius, 0, Math.PI * 2);
      this.ctx.fillStyle = `rgba(255, 165, 0, ${1 - exp.radius / exp.maxRadius})`;
      this.ctx.fill();
      
      // Inner explosion
      this.ctx.beginPath();
      this.ctx.arc(screenX, screenY, exp.radius * 0.6, 0, Math.PI * 2);
      this.ctx.fillStyle = `rgba(255, 255, 0, ${1 - exp.radius / exp.maxRadius})`;
      this.ctx.fill();
    }
  }

  private drawHUD(): void {
    const localTank = this.gameService.localTank();
    if (!localTank) return;
    
    const padding = 10;
    const boxWidth = 150;
    const boxHeight = 60;
    
    // HUD background
    this.ctx.fillStyle = 'rgba(0, 0, 0, 0.7)';
    this.ctx.fillRect(padding, padding, boxWidth, boxHeight);
    
    // Lives
    this.ctx.fillStyle = '#fff';
    this.ctx.font = '14px Arial';
    this.ctx.textAlign = 'left';
    this.ctx.fillText(`Lives: ${localTank.lives}`, padding + 10, padding + 20);
    this.ctx.fillText(`Health: ${localTank.health}/3`, padding + 10, padding + 40);
    
    // Tick counter (for debugging)
    this.ctx.fillStyle = '#888';
    this.ctx.font = '10px Arial';
    this.ctx.fillText(`Tick: ${this.gameService.currentTick()}`, padding + 10, padding + 55);
  }
}
