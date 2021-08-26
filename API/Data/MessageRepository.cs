using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;
using API.Helpers;
using API.Interfaces;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data
{
    public class MessageRepository : IMessageRepository
    {
        private readonly DataContext _context;
        private readonly IMapper _mapper;

        public MessageRepository(DataContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public void AddGroup(Group group)
        {
            _context.Groups.Add(group);
        }

        public void AddMessage(Message message)
        {
            _context.Messages.Add(message);
        }

        public void DeleteMessage(Message message)
        {
            _context.Messages.Remove(message);
        }

        public async Task<Connection> GetConnection(string connectionId)
        {
            return await _context.Connections.FindAsync(connectionId);
        }

        public async Task<Group> GetGroupForConnection(string connectionId)
        {
            return await _context.Groups
                .Include(c => c.Connections)
                .Where(c => c.Connections.Any(c => c.ConnectionId == connectionId))
                .FirstOrDefaultAsync();
        }

        public async Task<Message> GetMessage(int id)
        {
            return await _context.Messages.FindAsync(id);
        }

        public async Task<Group> GetMessageGroup(string groupName)
        {
            return await _context.Groups
                .Include(x => x.Connections)
                .FirstOrDefaultAsync(x => x.Name == groupName);
        }

        public async Task<PagedList<MessageDto>> GetMessagesForUser(MessageParams messageParams)
        {
            var query = _context.Messages
                .OrderByDescending(m => m.MessageSent)
                .ProjectTo<MessageDto>(_mapper.ConfigurationProvider)
                .AsQueryable();
            
            query = messageParams.Container switch
            {
                "Inbox" => query.Where(
                    u => u.RecipientUserName == messageParams.UserName
                    && u.RecipientDeleted == false
                    ),
                "Outbox" => query.Where(
                    u => u.SenderUserName == messageParams.UserName
                    && u.SenderDeleted == false
                    ),
                _ => query.Where(
                    u => u.RecipientUserName == messageParams.UserName
                    && u.DateRead == null
                    && u.RecipientDeleted == false
                    )
            };

            return await PagedList<MessageDto>.CreateAsync(
                    query,
                    messageParams.PageNumber,
                    messageParams.PageSize
                    );
        }

        public async Task<IEnumerable<MessageDto>> GetMessageThread(string currentUserName, string recipienUserName)
        {
            var messages = await _context.Messages
                .Where(
                    m => m.Recipient.UserName == currentUserName
                    && m.Sender.UserName == recipienUserName
                    && m.RecipientDeleted == false
                    || m.Recipient.UserName == recipienUserName
                    && m.Sender.UserName == currentUserName
                    && m.SenderDeleted == false
                )
                .OrderBy(m => m.MessageSent)
                .ProjectTo<MessageDto>(_mapper.ConfigurationProvider)
                .ToListAsync();
            
            var unreadMessages = messages
                .Where(m => m.DateRead == null && m.RecipientUserName == currentUserName)
                .ToList();

            if (unreadMessages.Any())
                foreach (var msg in unreadMessages) msg.DateRead = DateTime.UtcNow;

            return messages;
        }

        public void RemoveConnection(Connection connection)
        {
            _context.Connections.Remove(connection);
        }

    }
}