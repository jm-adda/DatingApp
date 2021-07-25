using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Helpers;
using API.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Authorize]
    public class MessagesController : BaseApiController
    {
        private readonly IUserRepository _userRepository;
        private readonly IMessageRepository _messageRepository;
        private readonly IMapper _mapper;
        public MessagesController(IUserRepository userRepository, IMessageRepository messageRepository, IMapper mapper)
        {
            _userRepository = userRepository;
            _messageRepository = messageRepository;
            _mapper = mapper;
        }

        [HttpPost]
        public async Task<ActionResult<MessageDto>> CreateMessage(CreateMessageDto createMessageDto)
        {
            var userName = User.GetUserName();

            var sender = await _userRepository.GetUserByNameAsync(userName);
            var recipient = await _userRepository.GetUserByNameAsync(createMessageDto.RecipientUserName);

            if (recipient == null) return NotFound();

            if (userName == recipient.UserName.ToLower())
                return BadRequest("Cannot send messages to yourself");

            var message = new Message
            {
                Sender = sender,
                SenderUserName = sender.UserName,
                Recipient = recipient,
                RecipientUserName = recipient.UserName,
                Content = createMessageDto.Content
            };

            _messageRepository.AddMessage(message);

            if (await _messageRepository.SaveAllAsync())
                return Ok(_mapper.Map<MessageDto>(message));

            return BadRequest("Failed to send message");
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<MessageDto>>> GetMessagesForUser([FromQuery] MessageParams messageParams)
        {
            messageParams.UserName = User.GetUserName();

            var messages = await _messageRepository.GetMessagesForUser(messageParams);

            Response.AddPaginationHeader(
                messages.CurrentPage,
                messages.PageSize,
                messages.TotalCount,
                messages.TotalPages
                );
            
            return messages;
        }

        [HttpGet("thread/{username}")]
        public async Task<ActionResult<IEnumerable<MessageDto>>> GetMessageThread(string userName)
        {
            var currentUserName = User.GetUserName();

            return Ok(await _messageRepository.GetMessageThread(currentUserName, userName));
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteMessage(int id)
        {
            var userName = User.GetUserName();
            var message = await _messageRepository.GetMessage(id);

            if (message.SenderUserName != userName && message.RecipientUserName != userName)
                return Unauthorized();
            
            if (message.SenderUserName == userName) message.SenderDeleted = true;
            if (message.RecipientUserName == userName) message.RecipientDeleted = true;
            
            if (message.SenderDeleted && message.RecipientDeleted)
                _messageRepository.DeleteMessage(message);
            
            if (await _messageRepository.SaveAllAsync()) return Ok();

            return BadRequest("Problem when deleting the message");
        }
    }
}