import { Component, inject, signal, OnInit, OnDestroy, computed, effect, ElementRef, viewChild } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';
import { GameService } from '../../services/game.service';
import { RoomService } from '../../services/room.service';
import { AuthService } from '../../services/auth.service';
import { RoomResponse, MapResponse, PlayerInfoDto, CreateRoomRequest, ChatMessage } from '../../models';

@Component({
  selector: 'app-waiting-room',
  imports: [CommonModule, RouterLink],
  templateUrl: './waiting-room.html',
  styleUrl: './waiting-room.scss',
})
export class WaitingRoom implements OnInit, OnDestroy {
  private gameService = inject(GameService);
  private roomService = inject(RoomService);
  private authService = inject(AuthService);
  private router = inject(Router);
  
  private subscriptions: Subscription[] = [];
  
  // Signal-based viewChild (Angular 17+)
  private chatMessagesContainer = viewChild<ElementRef<HTMLDivElement>>('chatMessagesContainer');

  // UI State
  isLoading = signal(false);
  errorMsg = signal<string | null>(null);
  
  // Room list
  availableRooms = signal<RoomResponse[]>([]);
  availableMaps = signal<MapResponse[]>([]);
  
  // Current room state
  currentRoomId = signal<string | null>(null);
  roomPlayers = signal<PlayerInfoDto[]>([]);
  canStartGame = signal(false);
  countdown = signal<number | null>(null);
  
  // Chat state
  chatInput = signal('');
  chatMessages = signal<ChatMessage[]>([]);
  
  // Form state
  selectedMapId = signal<string | null>(null);
  maxPlayers = signal(4);
  lives = signal(3);
  
  // Computed
  isInRoom = computed(() => this.currentRoomId() !== null);
  isHost = computed(() => this.gameService.isHost());
  isConnected = computed(() => this.gameService.isConnected());
  currentUsername = computed(() => this.gameService.username() ?? this.authService.getUsername() ?? 'Unknown');
  roomStatus = computed(() => this.gameService.roomStatus());

  constructor() {
    // Navigate to game when game starts
    effect(() => {
      const status = this.roomStatus();
      if (status === 'playing') {
        this.router.navigate(['/game']);
      }
    });
  }

  ngOnInit(): void {
    this.connectAndLoad();
  }

  ngOnDestroy(): void {
    this.subscriptions.forEach(sub => sub.unsubscribe());
  }

  private async connectAndLoad(): Promise<void> {
    try {
      this.isLoading.set(true);
      
      await this.gameService.connect();
      
      this.setupSubscriptions();
      
      await this.loadData();
      
      await this.gameService.subscribeToRooms();
      
    } catch (err) {
      console.error('Failed to connect:', err);
      this.errorMsg.set('Failed to connect to server');
    } finally {
      this.isLoading.set(false);
    }
  }

  private setupSubscriptions(): void {
    this.subscriptions.push(
      this.gameService.availableRooms$.subscribe(() => {
        this.loadRoomsFromHttp();
      })
    );

    this.subscriptions.push(
      this.gameService.onJoinedRoom$.subscribe(event => {
        this.currentRoomId.set(event.roomId);
        this.chatMessages.set([]);
        this.isLoading.set(false);
        this.gameService.getRoomState();
        this.setupInRoomSubscriptions();
      })
    );

    this.subscriptions.push(
      this.gameService.onError$.subscribe(error => {
        this.errorMsg.set(`${error.code}: ${error.message}`);
        this.isLoading.set(false);
      })
    );
  }

  private loadRoomsFromHttp(): void {
    this.roomService.getRooms().subscribe({
      next: rooms => this.availableRooms.set(rooms),
      error: err => console.error('Error loading rooms:', err)
    });
  }

  private setupInRoomSubscriptions(): void {
    this.subscriptions.push(
      this.gameService.onRoomState$.subscribe(state => {
        this.roomPlayers.set(state.players);
        this.canStartGame.set(state.canStart);
      })
    );

    this.subscriptions.push(
      this.gameService.onPlayerJoined$.subscribe(event => {
        this.roomPlayers.update(players => [...players, {
          playerId: event.tank.playerId,
          username: event.tank.username,
          isHost: false,
          isReady: true
        }]);
      })
    );

    this.subscriptions.push(
      this.gameService.onPlayerLeft$.subscribe(event => {
        this.roomPlayers.update(players => 
          players.filter(p => p.playerId !== event.playerId)
        );
      })
    );

    this.subscriptions.push(
      this.gameService.onCountdownUpdate$.subscribe(seconds => {
        this.countdown.set(seconds);
      })
    );

    this.subscriptions.push(
      this.gameService.onGameStarting$.subscribe(() => {
        this.countdown.set(3);
      })
    );

    this.subscriptions.push(
      this.gameService.onChatMessage$.subscribe(msg => {
        this.chatMessages.update(messages => [...messages, {
          username: msg.username,
          text: msg.text,
          timestamp: msg.timestamp
        }]);
        setTimeout(() => this.scrollChatToBottom(), 50);
      })
    );
  }

  private async loadData(): Promise<void> {
    this.roomService.getMaps().subscribe({
      next: maps => {
        this.availableMaps.set(maps);
        if (maps.length > 0 && !this.selectedMapId()) {
          this.selectedMapId.set(maps[0].id);
        }
      },
      error: err => console.error('Error loading maps:', err)
    });
  }

  refreshRooms(): void {
    this.gameService.subscribeToRooms()
      .catch(err => this.errorMsg.set('Failed to refresh rooms'));
  }

  createRoom(): void {
    const mapId = this.selectedMapId();
    if (!mapId) {
      this.errorMsg.set('Please select a map');
      return;
    }

    this.isLoading.set(true);
    this.errorMsg.set(null);

    const request: CreateRoomRequest = {
      mapId,
      maxPlayers: this.maxPlayers(),
      lives: this.lives()
    };

    this.roomService.createRoom(request).subscribe({
      next: async (room) => {
        // Join the room via SignalR
        try {
          await this.gameService.joinRoom(room.id);
        } catch (err) {
          this.errorMsg.set('Failed to join created room');
          this.isLoading.set(false);
        }
      },
      error: (err) => {
        this.isLoading.set(false);
        this.errorMsg.set(err.error?.message || 'Failed to create room');
      }
    });
  }

  async joinRoom(roomId: string): Promise<void> {
    this.isLoading.set(true);
    this.errorMsg.set(null);

    // First join via REST API (adds to DB)
    this.roomService.joinRoom(roomId).subscribe({
      next: async () => {
        // Then join via SignalR (adds to in-memory game)
        try {
          await this.gameService.joinRoom(roomId);
        } catch (err) {
          this.errorMsg.set('Failed to join room');
          this.isLoading.set(false);
        }
      },
      error: (err) => {
        this.isLoading.set(false);
        this.errorMsg.set(err.error?.message || 'Failed to join room');
      }
    });
  }

  async leaveRoom(): Promise<void> {
    const roomId = this.currentRoomId();
    if (!roomId) return;

    this.isLoading.set(true);

    try {
      await this.gameService.leaveRoom();
      this.roomService.leaveRoom(roomId).subscribe();
      this.currentRoomId.set(null);
      this.roomPlayers.set([]);
      this.chatMessages.set([]); // Clear chat when leaving
      this.refreshRooms();
    } catch (err) {
      this.errorMsg.set('Failed to leave room');
    } finally {
      this.isLoading.set(false);
    }
  }

  async startGame(): Promise<void> {
    if (!this.isHost() || !this.canStartGame()) return;

    this.isLoading.set(true);
    this.errorMsg.set(null);

    try {
      await this.gameService.startGame();
      // Game will start automatically when server sends GameStarting event
    } catch (err) {
      this.errorMsg.set('Failed to start game');
      this.isLoading.set(false);
    }
  }

  selectMap(mapId: string): void {
    this.selectedMapId.set(mapId);
  }

  setMaxPlayers(value: number): void {
    this.maxPlayers.set(Math.max(2, Math.min(4, value)));
  }

  setLives(value: number): void {
    this.lives.set(Math.max(1, Math.min(10, value)));
  }
  
  sendChat(): void {
    const text = this.chatInput().trim();
    if (!text) return;
    
    this.gameService.sendChatMessage(text);
    this.chatInput.set('');
  }
  
  private scrollChatToBottom(): void {
    const container = this.chatMessagesContainer();
    if (container) {
      const el = container.nativeElement;
      el.scrollTop = el.scrollHeight;
    }
  }
}
