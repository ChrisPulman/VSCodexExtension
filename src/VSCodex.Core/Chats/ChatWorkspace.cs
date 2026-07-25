// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace VSCodex.Core.Chats;

/// <summary>Registers independent chats and changes selection without cancelling background work.</summary>
public sealed class ChatWorkspace
{
    /// <summary>Synchronizes the chat registry and selected identifier.</summary>
    private readonly object _sync = new();

    /// <summary>Stores coordinators by stable local chat identifier.</summary>
    private readonly Dictionary<string, ChatRunCoordinator> _chats = new(StringComparer.Ordinal);

    /// <summary>Stores the selected local chat identifier.</summary>
    private string? _selectedChatId;

    /// <summary>Gets the selected local chat identifier.</summary>
    public string? SelectedChatId
    {
        get
        {
            lock (_sync)
            {
                return _selectedChatId;
            }
        }
    }

    /// <summary>Adds a chat and selects the first chat automatically.</summary>
    /// <param name="chat">The chat coordinator to register.</param>
    public void Add(ChatRunCoordinator chat)
    {
        if (chat is null)
        {
            throw new ArgumentNullException(nameof(chat));
        }

        lock (_sync)
        {
            if (_chats.ContainsKey(chat.ChatId))
            {
                throw new InvalidOperationException($"Chat '{chat.ChatId}' is already registered.");
            }

            _chats.Add(chat.ChatId, chat);
            _selectedChatId ??= chat.ChatId;
        }
    }

    /// <summary>Selects an existing chat without changing any chat's execution state.</summary>
    /// <param name="chatId">The stable local chat identifier.</param>
    /// <returns>The selected chat coordinator.</returns>
    public ChatRunCoordinator Select(string chatId)
    {
        lock (_sync)
        {
            if (!_chats.TryGetValue(chatId, out var chat))
            {
                throw new KeyNotFoundException($"Chat '{chatId}' is not registered.");
            }

            _selectedChatId = chatId;
            return chat;
        }
    }

    /// <summary>Gets a chat by its stable identifier.</summary>
    /// <param name="chatId">The stable local chat identifier.</param>
    /// <returns>The requested chat coordinator.</returns>
    public ChatRunCoordinator Get(string chatId)
    {
        lock (_sync)
        {
            return _chats.TryGetValue(chatId, out var chat)
                ? chat
                : throw new KeyNotFoundException($"Chat '{chatId}' is not registered.");
        }
    }
}
