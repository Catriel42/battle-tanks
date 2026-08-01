import { Component, inject, signal, DestroyRef } from '@angular/core';
import { FormField, form, required, minLength } from '@angular/forms/signals';
import { RouterLink } from '@angular/router';
import { Game } from '../../services/game';
import { PlayerInfo, ChatMessage } from '../../models';
import { PlayerStore } from '../../store/players.store';

@Component({
  selector: 'app-waiting-room',
  imports: [FormField, RouterLink],
  templateUrl: './waiting-room.html',
  styleUrl: './waiting-room.scss',
})
export class WaitingRoom {
  private gameService: Game = inject(Game);
  private destroyRef = inject(DestroyRef);
  playerStore = inject(PlayerStore);

  joined = signal(false);

  chatMessages = signal<ChatMessage[]>([]);
  chatInput = signal('');

  usernameModel = signal({ username: '' });
  usernameForm = form(this.usernameModel, (f) => {
    required(f.username, { message: 'Name is required' });
    minLength(f.username, 3, { message: 'Minimum 3 characters' });
  });

  constructor() {
    const chatSub = this.gameService.onChatMessage((msg: ChatMessage) => {
      this.chatMessages.update((current) => [...current, msg]);
    });

    this.destroyRef.onDestroy(() => {
      chatSub?.unsubscribe();
    });
  }

  join() {
    if (this.usernameForm().invalid()) return;

    const playerInfo: PlayerInfo = { 
      username: this.usernameModel().username.trim(),
      id: this.playerStore.localPlayerId() ?? undefined
    };
    this.playerStore.setLocalUsername(playerInfo.username);
    this.gameService.sendPlayerJoin(playerInfo);
    this.playerStore.addPlayer(playerInfo);
    this.joined.set(true);
  }

  sendChat() {
    const text = this.chatInput().trim();
    if (!text) return;

    const msg: ChatMessage = {
      username: this.usernameModel().username || 'Anonymous',
      text: text,
      timestamp: Date.now(),
    };

    this.gameService.sendChatMessage(msg);
    this.chatMessages.update((current) => [...current, msg]);
    this.chatInput.set('');
  }
}
