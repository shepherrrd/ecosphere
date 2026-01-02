# Ecosphere Frontend

A Next.js-based peer-to-peer video calling application with real-time communication capabilities.

## Features

- **Audio & Video Calls**: High-quality peer-to-peer calls using WebRTC
- **Multi-device Support**: Answer calls on any device, automatically stops ringing on others
- **Real-time Signaling**: SignalR for WebRTC negotiation
- **Contact Management**: Add and manage contacts
- **Responsive UI**: Modern, responsive interface with dark mode support
- **Screen Sharing**: Coming Soon

## Tech Stack

- **Next.js 15** - React framework with App Router
- **TypeScript** - Type-safe development
- **Tailwind CSS** - Utility-first CSS framework
- **SignalR** - Real-time communication for WebRTC signaling
- **WebRTC** - Peer-to-peer audio/video streaming

## Prerequisites

- Node.js 18+
- npm or yarn
- Running backend API (see backend README)

## Getting Started

1. **Install dependencies**:
   ```bash
   npm install
   ```

2. **Configure environment variables**:
   Copy `.env.local.example` to `.env.local` and update the values:
   ```env
   NEXT_PUBLIC_API_URL=http://localhost:5000/api
   NEXT_PUBLIC_HUB_URL=http://localhost:5000/callHub
   ```

3. **Run the development server**:
   ```bash
   npm run dev
   ```

4. **Open in browser**:
   Navigate to [http://localhost:3000](http://localhost:3000)

## Project Structure

```
frontend/
├── app/                      # Next.js App Router pages
│   ├── auth/                # Authentication page
│   ├── layout.tsx           # Root layout with providers
│   ├── page.tsx             # Home page (Dashboard)
│   └── globals.css          # Global styles
├── components/              # React components
│   ├── auth/               # Login/Register forms
│   ├── call/               # Call UI components
│   └── layout/             # Layout components (Dashboard)
├── lib/                    # Core logic
│   ├── contexts/          # React contexts (Auth, Call)
│   ├── services/          # API, SignalR, WebRTC services
│   └── types/             # TypeScript type definitions
└── public/                # Static assets
```

## Key Features Explained

### Authentication
- JWT-based authentication with refresh tokens
- Multi-device session tracking
- Automatic token refresh

### WebRTC Calling
- **Initiator Flow**: Get media → Create peer connection → Send offer → Receive answer → Connect
- **Receiver Flow**: Incoming call notification → Accept → Get media → Create peer connection → Receive offer → Send answer → Connect
- **ICE Candidates**: Exchanged via SignalR for NAT traversal
- **STUN/TURN**: Custom servers configured from backend

### Multi-device Support
- All user devices receive incoming call notifications
- When one device accepts, others automatically stop ringing
- WebRTC signaling sent only to the specific devices in the call (not broadcast to all devices)

### Call Controls
- Mute/unmute microphone
- Enable/disable video camera
- End call
- Screen sharing button (placeholder for future feature)

## Build for Production

```bash
npm run build
npm start
```

## Development

```bash
# Run development server
npm run dev

# Lint code
npm run lint

# Type check
npx tsc --noEmit
```

## Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `NEXT_PUBLIC_API_URL` | Backend API base URL | `http://localhost:5000/api` |
| `NEXT_PUBLIC_HUB_URL` | SignalR hub URL | `http://localhost:5000/callHub` |

## Browser Support

- Chrome/Edge 90+
- Firefox 88+
- Safari 14+

WebRTC requires HTTPS in production (except localhost).

## Troubleshooting

### Camera/Microphone Access Denied
- Check browser permissions
- Ensure HTTPS in production
- Grant permissions when prompted

### SignalR Connection Failed
- Verify backend is running
- Check CORS settings in backend
- Verify WebSocket support

### WebRTC Connection Failed
- Check STUN/TURN server configuration
- Verify firewall/NAT settings
- Check ICE candidate exchange in console

## License

MIT
