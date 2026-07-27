import { TestBed } from '@angular/core/testing';
import { Game } from './game';

describe('Game', () => {
  let service: Game;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(Game);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should have connect method', () => {
    expect(typeof service.connect).toBe('function');
  });

  it('should have disconnect method', () => {
    expect(typeof service.disconnect).toBe('function');
  });

  it('should have sendPlayerMove method', () => {
    expect(typeof service.sendPlayerMove).toBe('function');
  });

  it('should have onPlayerMove method', () => {
    expect(typeof service.onPlayerMove).toBe('function');
  });

  it('should have sendChatMessage method', () => {
    expect(typeof service.sendChatMessage).toBe('function');
  });

  it('should have onChatMessage method', () => {
    expect(typeof service.onChatMessage).toBe('function');
  });

  it('should have sendPlayerJoin method', () => {
    expect(typeof service.sendPlayerJoin).toBe('function');
  });

  it('should have onPlayerJoin method', () => {
    expect(typeof service.onPlayerJoin).toBe('function');
  });

  it('should have onGameState method', () => {
    expect(typeof service.onGameState).toBe('function');
  });

  it('should have getConnectionStatus$ method', () => {
    expect(typeof service.getConnectionStatus$).toBe('function');
  });

  it('should warn when sending without connection', () => {
    const warnSpy = vi.spyOn(console, 'warn');
    service.sendPlayerMove({ x: 10, y: 20 });
    expect(warnSpy).toHaveBeenCalledWith('[GameService] Cannot send message — not connected.');
    warnSpy.mockRestore();
  });
});
