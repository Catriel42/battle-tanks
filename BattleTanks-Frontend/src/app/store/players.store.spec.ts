import { describe, it, expect, beforeEach, vi } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { PlayerStore } from './players.store';
import { Game } from '../services/game';

describe('PlayerStore', () => {
  let store: any;
  let mockGameService: any;

  beforeEach(() => {
    mockGameService = {
      connect: vi.fn(),
      onWelcome: vi.fn().mockReturnValue({ unsubscribe: vi.fn() }),
      onPlayerJoin: vi.fn().mockReturnValue({ unsubscribe: vi.fn() }),
      onPlayerLeave: vi.fn().mockReturnValue({ unsubscribe: vi.fn() }),
      onPlayerMove: vi.fn().mockReturnValue({ unsubscribe: vi.fn() }),
    };

    TestBed.configureTestingModule({
      providers: [
        PlayerStore,
        { provide: Game, useValue: mockGameService }
      ]
    });
    store = TestBed.inject(PlayerStore);
  });

  it('should be created with empty state', () => {
    expect(store.players()).toEqual([]);
  });

  it('should add a player', () => {
    store.addPlayer({ username: 'Cato', id: 'uuid-cato' });
    expect(store.players().length).toBe(1);
    expect(store.players()[0].username).toBe('Cato');
    expect(store.players()[0].health).toBe(3);
  });

  it('should avoid adding duplicate players', () => {
    store.addPlayer({ username: 'Cato', id: 'uuid-cato' });
    store.addPlayer({ username: 'Cato', id: 'uuid-cato' });
    expect(store.players().length).toBe(1);
  });

  it('should update a player position', () => {
    store.addPlayer({ username: 'Cato', id: 'uuid-123' });
    store.updatePlayerPosition('uuid-123', { x: 50, y: 50 });
    expect(store.players()[0].position).toEqual({ x: 50, y: 50 });
  });

  it('should remove a player by id', () => {
    store.addPlayer({ username: 'Cato', id: 'uuid-cato' });
    expect(store.players().length).toBe(1);

    store.removePlayerById('uuid-cato');
    expect(store.players().length).toBe(0);
  });
});
