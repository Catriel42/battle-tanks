# Battle Tanks - Frontend

Angular 22 frontend for a multiplayer real-time tank battle game with server-authoritative game loop architecture.

## Quick Start

### Prerequisites

- Node.js 20+ and npm 10+
- Backend server running on `http://localhost:5000`

### Development Setup

1. **Install dependencies:**
   ```bash
   npm install
   ```

2. **Start development server:**
   ```bash
   npm start
   ```
   App will be available at `http://localhost:4200/`

3. **Build for production:**
   ```bash
   npm run build
   ```
   Output: `dist/BattleTanks-Frontend/`

## Architecture

### Server-Authoritative Game Loop

- **GameHub** (SignalR): Real-time communication with server
- **GameService**: Manages SignalR connections, state updates, and chat
- **Game Canvas**: Renders tank positions and bullets from server state
- **HUD Panel**: Displays player stats, health, and scoreboard

### Design System

All UI components use a centralized retro/arcade design system:

- **Tokens:** `src/styles/_variables.scss` - Colors, fonts, spacing, borders
- **Mixins:** `src/styles/_mixins.scss` - Reusable component patterns
- **Font:** Press Start 2P (pixel-perfect bitmap)
- **Palette:** Black, white, gray panels; red/green/yellow accents

### Key Files

```
src/
├── app/
│   ├── components/
│   │   ├── login/              # Auth UI (email, password)
│   │   ├── register/           # Registration UI
│   │   ├── waiting-room/       # Lobby (room list, chat, countdown)
│   │   ├── game/               # Main game with canvas and HUD
│   │   ├── game-canvas/        # Canvas rendering engine
│   │   └── hud-panel/          # Player stats sidebar
│   ├── services/
│   │   ├── game.service.ts     # SignalR client and handlers
│   │   ├── room.service.ts     # Room API endpoints
│   │   └── auth.service.ts     # Authentication
│   └── models/
│       └── game.models.ts      # DTOs and interfaces
└── styles/
    ├── _variables.scss        # Design tokens
    ├── _mixins.scss           # Component mixins
    └── styles.scss            # Global styles
```

## Development Workflow

### Creating a New Component

```bash
ng generate component components/my-component
```

New component SCSS should use design system:

```scss
@use '../../styles/variables' as *;
@use '../../styles/mixins' as *;

.my-component {
  @include panel;
  @include flex-column;
  gap: $space-4;
}
```

### Styling Guidelines

- Use variables from `_variables.scss`, never hardcode colors/spacing
- Use mixins from `_mixins.scss` for buttons, inputs, cards, panels
- No gradients, blur, or border-radius (retro aesthetic)
- Hard borders (2-6px black), pixel-perfect layout
- Font: Press Start 2P for all text

### Testing

```bash
# Run unit tests
npm test

# Run with coverage
npm test -- --coverage
```

## Common Commands

| Command | Purpose |
|---------|---------|
| `npm start` | Dev server (ng serve) |
| `npm run build` | Production build |
| `npm test` | Run unit tests |
| `npm run lint` | Lint code (if configured) |

## Troubleshooting

### Dev server won't connect to backend
- Ensure backend is running on `http://localhost:5000`
- Check `environment.ts` for correct API URL
- Verify SignalR hub is accessible

### Styles not applying
- Confirm `src/styles/_variables.scss` and `_mixins.scss` exist
- Check that component SCSS imports: `@use '../../../styles/variables' as *;`
- Run `npm run build` to check for SCSS errors

### Port 4200 already in use
```bash
ng serve --port 4300
```

## Architecture Notes

- **Game Loop:** Server authoritative (30 Hz), clients render smoothly between ticks
- **State Management:** Signals API (Angular 22+), no NgRx
- **Real-time:** SignalR for bi-directional communication
- **Canvas:** Native HTML5 canvas with optimized rendering

## More Info

- [Angular Docs](https://angular.dev)
- [Angular CLI](https://angular.dev/tools/cli)
- [SignalR Client](https://learn.microsoft.com/en-us/aspnet/core/signalr/javascript-client)
