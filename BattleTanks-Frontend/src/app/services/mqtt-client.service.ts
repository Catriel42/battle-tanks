import { Injectable, signal } from '@angular/core';
import { MqttService, IMqttMessage } from 'ngx-mqtt';
import { Observable, Subject } from 'rxjs';
import { PowerUpSpawnedEvent, PowerUpCollectedEvent, CollisionEvent, GameOverMqttEvent } from '../models/powerup.models';

@Injectable({
  providedIn: 'root'
})
export class MqttClientService {
  powerUps = signal<Map<string, any>>(new Map());
  
  private powerUpSpawned$ = new Subject<PowerUpSpawnedEvent>();
  private powerUpCollected$ = new Subject<PowerUpCollectedEvent>();
  private collision$ = new Subject<CollisionEvent>();
  private gameOver$ = new Subject<GameOverMqttEvent>();
  
  constructor(private mqttService: MqttService) {}
  
  subscribeToPowerUpSpawned(roomId: string): Observable<PowerUpSpawnedEvent> {
    const topic = `game/${roomId}/powerup/spawned`;
    
    this.mqttService.observe(topic).subscribe((msg: IMqttMessage) => {
      try {
        const event = JSON.parse(msg.payload.toString()) as PowerUpSpawnedEvent;
        this.powerUpSpawned$.next(event);
      } catch (e) {
        console.error('Failed to parse power-up spawned event', e);
      }
    });
    
    return this.powerUpSpawned$.asObservable();
  }
  
  subscribeToPowerUpCollected(roomId: string): Observable<PowerUpCollectedEvent> {
    const topic = `game/${roomId}/powerup/collected`;
    
    this.mqttService.observe(topic).subscribe((msg: IMqttMessage) => {
      try {
        const event = JSON.parse(msg.payload.toString()) as PowerUpCollectedEvent;
        this.powerUpCollected$.next(event);
      } catch (e) {
        console.error('Failed to parse power-up collected event', e);
      }
    });
    
    return this.powerUpCollected$.asObservable();
  }
  
  subscribeToCollisions(roomId: string): Observable<CollisionEvent> {
    const topic = `game/${roomId}/collision`;
    
    this.mqttService.observe(topic).subscribe((msg: IMqttMessage) => {
      try {
        const event = JSON.parse(msg.payload.toString()) as CollisionEvent;
        this.collision$.next(event);
      } catch (e) {
        console.error('Failed to parse collision event', e);
      }
    });
    
    return this.collision$.asObservable();
  }
  
  subscribeToGameOver(roomId: string): Observable<GameOverMqttEvent> {
    const topic = `game/${roomId}/gameover`;
    
    this.mqttService.observe(topic).subscribe((msg: IMqttMessage) => {
      try {
        const event = JSON.parse(msg.payload.toString()) as GameOverMqttEvent;
        this.gameOver$.next(event);
      } catch (e) {
        console.error('Failed to parse game-over event', e);
      }
    });
    
    return this.gameOver$.asObservable();
  }
  
  unsubscribeFromRoom(roomId: string): void {
    const topics = [
      `game/${roomId}/powerup/spawned`,
      `game/${roomId}/powerup/collected`,
      `game/${roomId}/collision`,
      `game/${roomId}/gameover`
    ];
    
    topics.forEach(topic => {
    });
  }
  
  calculateLatency(sentAt: number): number {
    return Date.now() - sentAt;
  }
}
