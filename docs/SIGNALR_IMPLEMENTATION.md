# SignalR Real-Time Communication Implementation Plan

## Overview
Implement real-time features using SignalR for trainer-client communication, goal assignments, and notifications.

## Backend (.NET) - SignalR Hubs

### 1. ChatHub - In-App Messaging

**File**: `backend/Mizan.Api/Hubs/ChatHub.cs`

```csharp
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Mizan.Infrastructure.Data;

[Authorize]
public class ChatHub : Hub
{
    private readonly MizanDbContext _db;

    public ChatHub(MizanDbContext db) => _db = db;

    public async Task SendMessage(string recipientId, string message)
    {
        var senderId = Context.User?.FindFirst("sub")?.Value;

        // Save to database
        var chatMessage = new ChatMessage
        {
            SenderId = Guid.Parse(senderId!),
            RecipientId = Guid.Parse(recipientId),
            Message = message,
            SentAt = DateTime.UtcNow
        };

        _db.ChatMessages.Add(chatMessage);
        await _db.SaveChangesAsync();

        // Send to recipient
        await Clients.User(recipientId).SendAsync("ReceiveMessage", new
        {
            senderId,
            message,
            sentAt = chatMessage.SentAt
        });

        // Confirm to sender
        await Clients.Caller.SendAsync("MessageSent", chatMessage.Id);
    }

    public async Task MarkAsRead(string messageId)
    {
        var message = await _db.ChatMessages.FindAsync(Guid.Parse(messageId));
        if (message != null)
        {
            message.ReadAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst("sub")?.Value;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
        await base.OnConnectedAsync();
    }
}
```

### 2. GoalHub - Goal Assignment

**File**: `backend/Mizan.Api/Hubs/GoalHub.cs`

```csharp
[Authorize(Roles = "trainer")]
public class GoalHub : Hub
{
    private readonly MizanDbContext _db;

    public GoalHub(MizanDbContext db) => _db = db;

    public async Task AssignGoal(string clientId, GoalDto goal)
    {
        var trainerId = Context.User?.FindFirst("sub")?.Value;

        // Verify trainer-client relationship with goal edit permission
        var relationship = await _db.TrainerClientRelationships
            .FirstOrDefaultAsync(r =>
                r.TrainerId == Guid.Parse(trainerId!) &&
                r.ClientId == Guid.Parse(clientId) &&
                r.Status == "active" &&
                r.EditGoals == true
            );

        if (relationship == null)
        {
            throw new HubException("Not authorized to assign goals to this client");
        }

        // Create goal (reuse existing command)
        var newGoal = new UserGoal
        {
            UserId = Guid.Parse(clientId),
            GoalType = goal.Type,
            TargetCalories = goal.TargetCalories,
            TargetProteinGrams = goal.TargetProteinGrams,
            TargetCarbsGrams = goal.TargetCarbsGrams,
            TargetFatGrams = goal.TargetFatGrams,
            TargetDate = goal.TargetDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.UserGoals.Add(newGoal);
        await _db.SaveChangesAsync();

        // Notify client
        await Clients.User(clientId).SendAsync("GoalAssigned", newGoal);

        // Confirm to trainer
        await Clients.Caller.SendAsync("GoalAssignmentSuccess", newGoal.Id);
    }

    public async Task UpdateGoalProgress(string goalId, decimal value, string notes)
    {
        var userId = Context.User?.FindFirst("sub")?.Value;

        var goal = await _db.UserGoals.FindAsync(Guid.Parse(goalId));
        if (goal?.UserId.ToString() != userId)
        {
            throw new HubException("Not authorized");
        }

        // Record progress (reuse existing command logic)
        var progress = new GoalProgress
        {
            UserGoalId = goal.Id,
            UserId = Guid.Parse(userId!),
            ActualCalories = (int)value, // Simplified - actual implementation would have all macros
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Notes = notes,
            CreatedAt = DateTime.UtcNow
        };

        _db.GoalProgress.Add(progress);
        await _db.SaveChangesAsync();

        // Notify trainer if goal was assigned by them
        var relationship = await _db.TrainerClientRelationships
            .FirstOrDefaultAsync(r => r.ClientId == goal.UserId && r.Status == "active");

        if (relationship != null)
        {
            await Clients.User(relationship.TrainerId.ToString()).SendAsync("ClientProgressUpdate", new
            {
                clientId = userId,
                goalId,
                value,
                notes
            });
        }
    }
}
```

### 3. Configuration in Program.cs

```csharp
// Add SignalR with Redis backplane for horizontal scaling
builder.Services.AddSignalR()
    .AddStackExchangeRedis(options =>
    {
        options.Configuration.EndPoints.Add("redis:6379");
    });

// Map hubs
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<GoalHub>("/hubs/goals");
app.MapHub<NotificationHub>("/hubs/notifications");
```

## Frontend - SignalR Client

### 1. Chat Service

**File**: `frontend/lib/services/signalr-chat.ts`

```typescript
import * as signalR from "@microsoft/signalr";
import { getApiToken } from "@/lib/auth-client";

export interface ChatMessage {
  id: string;
  senderId: string;
  recipientId: string;
  message: string;
  sentAt: string;
  readAt?: string;
}

export class ChatService {
  private connection: signalR.HubConnection | null = null;

  async connect(): Promise<void> {
    const apiUrl = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";
    const token = await getApiToken();

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(`${apiUrl}/hubs/chat`, {
        accessTokenFactory: () => token || "",
      })
      .withAutomaticReconnect()
      .build();

    await this.connection.start();
  }

  async sendMessage(recipientId: string, message: string): Promise<void> {
    if (!this.connection) throw new Error("Not connected");
    await this.connection.invoke("SendMessage", recipientId, message);
  }

  onMessageReceived(callback: (message: ChatMessage) => void): void {
    if (!this.connection) throw new Error("Not connected");
    this.connection.on("ReceiveMessage", callback);
  }

  async markAsRead(messageId: string): Promise<void> {
    if (!this.connection) throw new Error("Not connected");
    await this.connection.invoke("MarkAsRead", messageId);
  }

  disconnect(): void {
    this.connection?.stop();
  }
}

export const chatService = new ChatService();
```

### 2. Chat Hook

**File**: `frontend/hooks/useChat.ts`

```typescript
import { useState, useEffect } from "react";
import { chatService, ChatMessage } from "@/lib/services/signalr-chat";

export function useChat(recipientId?: string) {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [isConnected, setIsConnected] = useState(false);

  useEffect(() => {
    const connect = async () => {
      await chatService.connect();
      setIsConnected(true);

      chatService.onMessageReceived((message) => {
        if (!recipientId || message.senderId === recipientId) {
          setMessages((prev) => [...prev, message]);
        }
      });
    };

    connect();

    return () => {
      chatService.disconnect();
    };
  }, [recipientId]);

  async function sendMessage(message: string) {
    if (!recipientId) throw new Error("No recipient");
    await chatService.sendMessage(recipientId, message);
  }

  return { messages, isConnected, sendMessage };
}
```

### 3. Chat Page

**File**: `frontend/app/(dashboard)/chat/[userId]/page.tsx`

```typescript
"use client";

import { useChat } from "@/hooks/useChat";
import { useState } from "react";
import { useParams } from "next/navigation";

export default function ChatPage() {
  const params = useParams();
  const userId = params.userId as string;
  const { messages, isConnected, sendMessage } = useChat(userId);
  const [input, setInput] = useState("");

  async function handleSend() {
    if (!input.trim()) return;
    await sendMessage(input);
    setInput("");
  }

  return (
    <div className="flex flex-col h-screen max-w-4xl mx-auto">
      <div className="flex-1 overflow-y-auto p-4 space-y-4">
        {messages.map((msg) => (
          <div
            key={msg.id}
            className={`flex ${msg.senderId === userId ? "justify-start" : "justify-end"}`}
          >
            <div className={`card p-3 max-w-md ${
              msg.senderId === userId ? "bg-slate-100" : "bg-brand-500 text-white"
            }`}>
              <p>{msg.message}</p>
              <span className="text-xs opacity-70">
                {new Date(msg.sentAt).toLocaleTimeString()}
              </span>
            </div>
          </div>
        ))}
      </div>

      <div className="border-t p-4 flex gap-2">
        <input
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyPress={(e) => e.key === "Enter" && handleSend()}
          className="input flex-1"
          placeholder="Type a message..."
          disabled={!isConnected}
        />
        <button onClick={handleSend} disabled={!isConnected} className="btn-primary">
          Send
        </button>
      </div>
    </div>
  );
}
```

## Database Schema

### ChatMessage Table (Already Exists)

```sql
CREATE TABLE chat_messages (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    conversation_id UUID NOT NULL REFERENCES chat_conversations(id),
    sender_id UUID NOT NULL REFERENCES users(id),
    recipient_id UUID NOT NULL REFERENCES users(id),
    message TEXT NOT NULL,
    sent_at TIMESTAMPTZ DEFAULT NOW(),
    read_at TIMESTAMPTZ,
    is_deleted BOOLEAN DEFAULT FALSE
);

CREATE INDEX idx_chat_messages_conversation ON chat_messages(conversation_id, sent_at DESC);
CREATE INDEX idx_chat_messages_sender ON chat_messages(sender_id);
CREATE INDEX idx_chat_messages_recipient ON chat_messages(recipient_id);
```

## Next Steps for Implementation

1. **Backend**:
   - Create `ChatHub.cs` and `GoalHub.cs`
   - Add hub configuration to `Program.cs`
   - Test with SignalR test client

2. **Frontend**:
   - Implement `signalr-chat.ts` service
   - Create `useChat` hook
   - Build chat UI page
   - Add real-time notifications component

3. **Testing**:
   - Test message delivery
   - Test reconnection logic
   - Test multiple concurrent connections
   - Load test with Redis backplane

## Benefits

- ✅ Real-time trainer-client communication
- ✅ Instant goal assignment notifications
- ✅ Live progress updates
- ✅ Scalable with Redis backplane
- ✅ Automatic reconnection
- ✅ Read receipts and message history
