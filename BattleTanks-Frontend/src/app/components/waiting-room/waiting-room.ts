import { Component, inject, signal, DestroyRef, OnInit } from '@angular/core';
import { FormField, form, required } from '@angular/forms/signals';
import { RouterLink } from '@angular/router';
import { Game } from '../../services/game';
import { ChatMessage } from '../../models';
import { PlayerStore } from '../../store/players.store';
import { RoomService, Room } from '../../services/room.service';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-waiting-room',
  imports: [FormField, RouterLink],
  templateUrl: './waiting-room.html',
  styleUrl: './waiting-room.scss',
})
export class WaitingRoom implements OnInit {
  private gameService: Game = inject(Game);
  private roomService = inject(RoomService);
  private authService = inject(AuthService);
  private destroyRef = inject(DestroyRef);
  playerStore = inject(PlayerStore);

  joined = signal(false);
  isLoading = signal(false);
  errorMsg = signal<string | null>(null);


  chatInput = signal('');
  
  availableRooms = signal<Room[]>([]);
  currentUsername = signal<string>('');

  mapNameModel = signal({ mapName: '' });
  mapNameForm = form(this.mapNameModel, (f) => {
    required(f.mapName, { message: 'Map name is required' });
  });


  ngOnInit(): void {
    const username = this.authService.getUsername();
    if (username) {
      this.currentUsername.set(username);
    }
    this.fetchRooms();
  }

  fetchRooms(): void {
    this.roomService.getRooms().subscribe({
      next: (rooms) => this.availableRooms.set(rooms),
      error: (err) => console.error('Error fetching rooms', err)
    });
  }

  createRoom(): void {
    if (this.mapNameForm().invalid()) {
      this.errorMsg.set('Please enter a valid map name.');
      return;
    }
    
    this.isLoading.set(true);
    this.errorMsg.set(null);
    const mapName = this.mapNameModel().mapName.trim();

    this.roomService.createRoom(mapName, 4).subscribe({
      next: (room) => {
        this.isLoading.set(false);
        this.fetchRooms();
      },
      error: (err) => {
        this.isLoading.set(false);
        this.errorMsg.set('Failed to create room.');
      }
    });
  }

  joinRoom(roomId: string): void {
    this.isLoading.set(true);
    this.errorMsg.set(null);

    this.roomService.joinRoom(roomId).subscribe({
      next: () => {
        this.isLoading.set(false);
        const username = this.currentUsername();
        this.playerStore.setLocalUsername(username);
        this.gameService.sendPlayerJoin(username);
        this.playerStore.addPlayer({ username, id: this.playerStore.localPlayerId() ?? undefined });
        this.joined.set(true);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.errorMsg.set(err.error?.message || 'Failed to join room (might be full).');
      }
    });
  }

  sendChat() {
    const text = this.chatInput().trim();
    if (!text) return;

    const msg: ChatMessage = {
      username: this.currentUsername(),
      text: text,
      timestamp: Date.now(),
    };

    this.gameService.sendChatMessage(msg);
    this.chatInput.set('');
  }

  runBenchmark(): void {
    if (!this.joined()) {
      return;
    }
    
    let pingsSent = 0;
    const maxPings = 10;
    const rtts: number[] = [];
    
    const sub = this.gameService.onPong((timestamp: number) => {
      const rtt = Date.now() - timestamp;
      rtts.push(rtt);
      
      if (rtts.length === maxPings) {
        sub.unsubscribe();
        const avg = rtts.reduce((a, b) => a + b, 0) / maxPings;
        const min = Math.min(...rtts);
        const max = Math.max(...rtts);
        console.log(`[Benchmark] RTT over ${maxPings} pings: Avg=${avg}ms, Min=${min}ms, Max=${max}ms`);
        this.errorMsg.set(`Benchmark complete. Avg RTT: ${avg.toFixed(2)}ms`);
      }
    });

    const interval = setInterval(() => {
      if (pingsSent >= maxPings) {
        clearInterval(interval);
        return;
      }
      this.gameService.sendPing();
      pingsSent++;
    }, 200);
  }
}
