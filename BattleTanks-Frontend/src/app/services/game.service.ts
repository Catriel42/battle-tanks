import { Injectable, inject, signal, computed } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';

import { AuthService } from './auth.service';
import { MqttClientService } from './mqtt-client.service';
import { environment } from '../../environments/environment';
import {
  PlayerInputDto,
  GameStateSnapshot,
  ConnectedEvent,
  JoinedRoomEvent,
  GameStartingEvent,
  GameStartedEvent,
  PlayerJoinedEvent,
  PlayerLeftEvent,
  PlayerHitEvent,
  PlayerKilledEvent,
  PlayerEliminatedEvent,
  PlayerRespawnedEvent,
  BlockDestroyedEvent,
  BulletFiredEvent,
  GameOverEvent,
  RoomStateEvent,
  ErrorEvent,
  TankDto,
  BulletDto,
  Direction,
  GameStatus,
} from '../models';
import { PowerUpState, PowerUpSpawnedEvent, PowerUpCollectedEvent } from '../models/powerup.models';

@Injectable({
  providedIn: 'root'
})
export class GameService {
  private authService = inject(AuthService);
  private mqttService = inject(MqttClientService);
  
  private hubConnection: signalR.HubConnection | null = null;
  private readonly HUB_URL = environment.hubUrl;
  
  // Connection state
  private _isConnected = signal(false);
  private _connectionId = signal<string | null>(null);
  private _playerId = signal<string | null>(null);
  private _username = signal<string | null>(null);
  
  // Room state
  private _currentRoomId = signal<string | null>(null);
  private _roomStatus = signal<GameStatus>('waiting');
  private _isHost = signal(false);
  
  // Game state (from server snapshots)
  private _currentTick = signal(0);
  private _tanks = signal<TankDto[]>([]);
  private _bullets = signal<BulletDto[]>([]);
  private _destroyedBlocks = signal<Set<string>>(new Set());
  private _mapGrid = signal<number[][]>([]);
  private _mapWidth = signal(30);
  private _mapHeight = signal(20);
  
  // Power-ups
  private _powerUps = signal<Map<string, PowerUpState>>(new Map());
  
  // Computed signals
  readonly isConnected = this._isConnected.asReadonly();
  readonly connectionId = this._connectionId.asReadonly();
  readonly playerId = this._playerId.asReadonly();
  readonly username = this._username.asReadonly();
  readonly currentRoomId = this._currentRoomId.asReadonly();
  readonly roomStatus = this._roomStatus.asReadonly();
  readonly isHost = this._isHost.asReadonly();
  readonly currentTick = this._currentTick.asReadonly();
  readonly tanks = this._tanks.asReadonly();
  readonly bullets = this._bullets.asReadonly();
  readonly destroyedBlocks = this._destroyedBlocks.asReadonly();
  readonly mapGrid = this._mapGrid.asReadonly();
  readonly mapWidth = this._mapWidth.asReadonly();
  readonly mapHeight = this._mapHeight.asReadonly();
  readonly powerUps = this._powerUps.asReadonly();
  
  // Local tank (computed from tanks array)
  readonly localTank = computed(() => {
    const connId = this._connectionId();
    if (!connId) return null;
    return this._tanks().find(t => t.id === connId) ?? null;
  });
  
  // Enemy tanks (computed)
  readonly enemyTanks = computed(() => {
    const connId = this._connectionId();
    if (!connId) return this._tanks();
    return this._tanks().filter(t => t.id !== connId);
  });
  
  // Event subjects for components to subscribe
  private readonly connected$ = new Subject<ConnectedEvent>();
  private readonly joinedRoom$ = new Subject<JoinedRoomEvent>();
  private readonly gameStarting$ = new Subject<GameStartingEvent>();
  private readonly gameStarted$ = new Subject<GameStartedEvent>();
  private readonly gameState$ = new Subject<GameStateSnapshot>();
  private readonly playerJoined$ = new Subject<PlayerJoinedEvent>();
  private readonly playerLeft$ = new Subject<PlayerLeftEvent>();
  private readonly playerHit$ = new Subject<PlayerHitEvent>();
  private readonly playerKilled$ = new Subject<PlayerKilledEvent>();
  private readonly playerEliminated$ = new Subject<PlayerEliminatedEvent>();
  private readonly playerRespawned$ = new Subject<PlayerRespawnedEvent>();
  private readonly blockDestroyed$ = new Subject<BlockDestroyedEvent>();
  private readonly bulletFired$ = new Subject<BulletFiredEvent>();
  private readonly gameOver$ = new Subject<GameOverEvent>();
  private readonly roomState$ = new Subject<RoomStateEvent>();
  private readonly error$ = new Subject<ErrorEvent>();
  private readonly countdownUpdate$ = new Subject<number>();
  private readonly pong$ = new Subject<number>();
  private readonly chatMessage$ = new Subject<{ username: string; text: string; timestamp: number }>();
  
  // Public observables
  readonly onConnected$ = this.connected$.asObservable();
  readonly onJoinedRoom$ = this.joinedRoom$.asObservable();
  readonly onGameStarting$ = this.gameStarting$.asObservable();
  readonly onGameStarted$ = this.gameStarted$.asObservable();
  readonly onGameState$ = this.gameState$.asObservable();
  readonly onPlayerJoined$ = this.playerJoined$.asObservable();
  readonly onPlayerLeft$ = this.playerLeft$.asObservable();
  readonly onPlayerHit$ = this.playerHit$.asObservable();
  readonly onPlayerKilled$ = this.playerKilled$.asObservable();
  readonly onPlayerEliminated$ = this.playerEliminated$.asObservable();
  readonly onPlayerRespawned$ = this.playerRespawned$.asObservable();
  readonly onBlockDestroyed$ = this.blockDestroyed$.asObservable();
  readonly onBulletFired$ = this.bulletFired$.asObservable();
  readonly onGameOver$ = this.gameOver$.asObservable();
  readonly onRoomState$ = this.roomState$.asObservable();
  readonly onError$ = this.error$.asObservable();
  readonly onCountdownUpdate$ = this.countdownUpdate$.asObservable();
  readonly onPong$ = this.pong$.asObservable();
  readonly onChatMessage$ = this.chatMessage$.asObservable();
  
  // Input sequence counter
  private inputSequence = 0;
  
  /**
   * Connect to the SignalR hub with JWT authentication
   */
  async connect(): Promise<void> {
    if (this.hubConnection) {
      await this.disconnect();
    }
    
    const token = this.authService.getToken();
    if (!token) {
      console.error('[GameService] No JWT token available');
      throw new Error('Not authenticated');
    }
    
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${this.HUB_URL}?access_token=${token}`)
      .withAutomaticReconnect([0, 1000, 2000, 5000, 10000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();
    
    this.registerEventHandlers();
    
    this.hubConnection.onreconnecting(() => {
      console.log('[GameService] Reconnecting...');
      this._isConnected.set(false);
    });
    
    this.hubConnection.onreconnected(() => {
      console.log('[GameService] Reconnected');
      this._isConnected.set(true);
    });
    
    this.hubConnection.onclose(() => {
      console.log('[GameService] Connection closed');
      this._isConnected.set(false);
      this.resetState();
    });
    
    try {
      await this.hubConnection.start();
      console.log('[GameService] Connected to SignalR hub');
      this._isConnected.set(true);
    } catch (err) {
      console.error('[GameService] Connection failed:', err);
      throw err;
    }
  }
  
  /**
   * Disconnect from the hub
   */
  async disconnect(): Promise<void> {
    if (this.hubConnection) {
      await this.hubConnection.stop();
      this.hubConnection = null;
    }
    this.resetState();
  }
  
  /**
   * Join a game room
   */
  async joinRoom(roomId: string): Promise<void> {
    if (!this.hubConnection) {
      throw new Error('Not connected');
    }
    
    this._currentRoomId.set(roomId);
    
    this.subscribeMqttEvents(roomId);
    
    try {
      await this.hubConnection.invoke('GetEventHistory');
    } catch (e) {
      console.warn('[GameService] Failed to get event history:', e);
    }
    
    await this.hubConnection.invoke('JoinRoom', roomId);
  }
  
  /**
   * Leave the current room
   */
  async leaveRoom(): Promise<void> {
    if (!this.hubConnection) return;
    
    const roomId = this._currentRoomId();
    await this.hubConnection.invoke('LeaveRoom');
    this._currentRoomId.set(null);
    this._roomStatus.set('waiting');
    this._powerUps.set(new Map());
    
    if (roomId) {
      this.mqttService.unsubscribeFromRoom(roomId);
    }
  }
  
  /**
   * Start the game (host only)
   */
  async startGame(): Promise<void> {
    if (!this.hubConnection) return;
    await this.hubConnection.invoke('StartGame');
  }
  
  /**
   * Send player input to server
   */
  sendInput(type: 'move_start' | 'move_stop' | 'shoot', direction?: Direction): void {
    if (!this.hubConnection || this._roomStatus() !== 'playing') return;
    
    const input: PlayerInputDto = {
      type,
      direction,
      sequenceNumber: ++this.inputSequence
    };
    
    this.hubConnection.invoke('SendInput', input).catch(err => {
      console.error('[GameService] Failed to send input:', err);
    });
  }
  
  /**
   * Send move start input
   */
  moveStart(direction: Direction): void {
    this.sendInput('move_start', direction);
  }
  
  /**
   * Send move stop input
   */
  moveStop(): void {
    this.sendInput('move_stop');
  }
  
  /**
   * Send shoot input
   */
  shoot(): void {
    this.sendInput('shoot');
  }
  
  /**
   * Ping for latency measurement
   */
  ping(): void {
    this.hubConnection?.invoke('Ping');
  }
  
  /**
   * Request current room state
   */
  getRoomState(): void {
    this.hubConnection?.invoke('GetRoomState');
  }
  
  /**
   * Send a chat message to the room
   */
  sendChatMessage(text: string): void {
    if (!text.trim()) return;
    this.hubConnection?.invoke('SendChatMessage', { text: text.trim() });
  }
  
  /**
   * Check if a block is destroyed
   */
  isBlockDestroyed(row: number, col: number): boolean {
    return this._destroyedBlocks().has(`${row},${col}`);
  }
  
  /**
   * Get effective tile value (considering destroyed blocks)
   */
  getTileAt(row: number, col: number): number {
    if (this.isBlockDestroyed(row, col)) return 0;
    const grid = this._mapGrid();
    if (row < 0 || row >= grid.length) return 1;
    if (col < 0 || col >= (grid[0]?.length ?? 0)) return 1;
    return grid[row][col];
  }
  
  private registerEventHandlers(): void {
    if (!this.hubConnection) return;
    
    // Connection events
    this.hubConnection.on('Connected', (event: ConnectedEvent) => {
      this._connectionId.set(event.connectionId);
      this._playerId.set(event.playerId);
      this._username.set(event.username);
      this.connected$.next(event);
    });
    
    // Room events
    this.hubConnection.on('JoinedRoom', (event: JoinedRoomEvent) => {
      this._currentRoomId.set(event.roomId);
      this._mapGrid.set(event.mapGrid);
      this._mapWidth.set(event.mapWidth);
      this._mapHeight.set(event.mapHeight);
      this._roomStatus.set(event.status);
      this._tanks.set([event.tank]);
      this.joinedRoom$.next(event);
    });
    
    this.hubConnection.on('LeftRoom', () => {
      this._currentRoomId.set(null);
      this._roomStatus.set('waiting');
      this.resetGameState();
    });
    
    this.hubConnection.on('RoomState', (event: RoomStateEvent) => {
      this._roomStatus.set(event.status);
      this._isHost.set(event.hostId === this._connectionId());
      this.roomState$.next(event);
    });
    
    this.hubConnection.on('PlayerJoined', (event: PlayerJoinedEvent) => {
      this._tanks.update(tanks => [...tanks, event.tank]);
      this.playerJoined$.next(event);
    });
    
    this.hubConnection.on('PlayerLeft', (event: PlayerLeftEvent) => {
      this._tanks.update(tanks => tanks.filter(t => t.id !== event.playerId));
      this.playerLeft$.next(event);
    });
    
    this.hubConnection.on('HostChanged', (event: { newHostId: string }) => {
      this._isHost.set(event.newHostId === this._connectionId());
    });
    
    // Game lifecycle events
    this.hubConnection.on('GameStarting', (event: GameStartingEvent) => {
      this._roomStatus.set('starting');
      this._mapGrid.set(event.mapGrid);
      this._mapWidth.set(event.mapWidth);
      this._mapHeight.set(event.mapHeight);
      this._tanks.set(event.tanks);
      this.gameStarting$.next(event);
    });
    
    this.hubConnection.on('CountdownUpdate', (seconds: number) => {
      this.countdownUpdate$.next(seconds);
    });
    
    this.hubConnection.on('GameStarted', (event: GameStartedEvent) => {
      this._roomStatus.set('playing');
      this._tanks.set(event.tanks);
      this._currentTick.set(0);
      this._bullets.set([]);
      this._destroyedBlocks.set(new Set());
      this.gameStarted$.next(event);
    });
    
    // Game state snapshots (main game loop updates)
    this.hubConnection.on('GameState', (snapshot: GameStateSnapshot) => {
      this._currentTick.set(snapshot.tick);
      this._roomStatus.set(snapshot.status);
      this._tanks.set(snapshot.tanks);
      this._bullets.set(snapshot.bullets);
      
      // Update destroyed blocks from flattened array
      const destroyed = new Set<string>();
      for (let i = 0; i < snapshot.destroyedBlocks.length; i += 2) {
        const row = snapshot.destroyedBlocks[i];
        const col = snapshot.destroyedBlocks[i + 1];
        destroyed.add(`${row},${col}`);
      }
      this._destroyedBlocks.set(destroyed);
      
      this.gameState$.next(snapshot);
    });
    
    // Combat events
    this.hubConnection.on('PlayerHit', (event: PlayerHitEvent) => {
      this.playerHit$.next(event);
    });
    
    this.hubConnection.on('PlayerKilled', (event: PlayerKilledEvent) => {
      this.playerKilled$.next(event);
    });
    
    this.hubConnection.on('PlayerEliminated', (event: PlayerEliminatedEvent) => {
      this.playerEliminated$.next(event);
    });
    
    this.hubConnection.on('PlayerRespawned', (event: PlayerRespawnedEvent) => {
      this.playerRespawned$.next(event);
    });
    
    this.hubConnection.on('BlockDestroyed', (event: BlockDestroyedEvent) => {
      this._destroyedBlocks.update(blocks => {
        const newBlocks = new Set(blocks);
        newBlocks.add(`${event.row},${event.col}`);
        return newBlocks;
      });
      this.blockDestroyed$.next(event);
    });
    
    this.hubConnection.on('BulletFired', (event: BulletFiredEvent) => {
      this.bulletFired$.next(event);
    });
    
    // Game over
    this.hubConnection.on('GameOver', (event: GameOverEvent) => {
      this._roomStatus.set('finished');
      this.gameOver$.next(event);
    });
    
    // Error handling
    this.hubConnection.on('Error', (event: ErrorEvent) => {
      console.error('[GameService] Server error:', event.code, event.message);
      this.error$.next(event);
    });
    
    // Ping/pong
    this.hubConnection.on('Pong', (timestamp: number) => {
      this.pong$.next(timestamp);
    });
    
    // Chat messages
    this.hubConnection.on('ChatMessage', (event: { username: string; text: string; timestamp: number }) => {
      this.chatMessage$.next(event);
    });
    
    // Event history (from Redis - for recovery and benchmarking)
    this.hubConnection.on('EventHistory', (data: any) => {
      console.log('[GameService] Received event history:', data.Events?.length || 0, 'events');
      // Events are stored for potential recovery or analysis
      // For benchmarking, calculate latency: receivedAt - sent_at from each event
      if (data.Events && data.Events.length > 0) {
        data.Events.forEach((eventJson: string) => {
          try {
            const event = JSON.parse(eventJson);
            const latency = data.ReceivedAt - event.sent_at;
            console.debug('[GameService] Event latency:', event.type, latency + 'ms');
          } catch (e) {
            console.warn('[GameService] Failed to parse event from history', e);
          }
        });
      }
    });
  }
  
  private resetState(): void {
    this._isConnected.set(false);
    this._connectionId.set(null);
    this._currentRoomId.set(null);
    this.resetGameState();
  }
  
  private resetGameState(): void {
    this._roomStatus.set('waiting');
    this._currentTick.set(0);
    this._tanks.set([]);
    this._bullets.set([]);
    this._destroyedBlocks.set(new Set());
    this._mapGrid.set([]);
    this._powerUps.set(new Map());
    this._isHost.set(false);
    this.inputSequence = 0;
  }
  
  private subscribeMqttEvents(roomId: string): void {
    this.mqttService.subscribeToPowerUpSpawned(roomId).subscribe((event: PowerUpSpawnedEvent) => {
      const map = new Map(this._powerUps());
      map.set(event.id, {
        id: event.id,
        type: 'ExtraLife',
        x: event.x,
        y: event.y,
        isCollected: false
      });
      this._powerUps.set(map);
      console.log('[GameService] Power-up spawned:', event);
    });
    
    this.mqttService.subscribeToPowerUpCollected(roomId).subscribe((event: PowerUpCollectedEvent) => {
      const map = new Map(this._powerUps());
      map.delete(event.id);
      this._powerUps.set(map);
      console.log('[GameService] Power-up collected by', event.username, '- New lives:', event.new_lives);
    });
  }

  async subscribeToRooms(): Promise<void> {
    if (!this.hubConnection) return;
    
    this.hubConnection.off('RoomsChanged');
    this.hubConnection.on('RoomsChanged', () => {
      console.log('[GameService] Rooms changed notification received');
      this.availableRooms.next(null);
    });

    try {
      await this.hubConnection.invoke('SubscribeToRooms');
      console.log('[GameService] Subscribed to rooms');
    } catch (err) {
      console.error('[GameService] Failed to subscribe to rooms:', err);
    }
  }

  async unsubscribeFromRooms(): Promise<void> {
    if (!this.hubConnection) return;
    try {
      await this.hubConnection.invoke('UnsubscribeFromRooms');
      this.hubConnection.off('RoomsChanged');
    } catch (err) {
      console.error('[GameService] Failed to unsubscribe from rooms:', err);
    }
  }

  get availableRooms$() {
    return this.availableRooms.asObservable();
  }

  private availableRooms = new Subject<any>();
}

