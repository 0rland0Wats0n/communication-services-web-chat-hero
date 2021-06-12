﻿// © Microsoft Corporation. All rights reserved.

using Azure;
using Azure.Communication;
using Azure.Communication.Chat;
using Azure.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;

namespace Chat
{
	/// <summary>
	/// To enable test clients to chat in the same thread
	/// </summary>
	[ApiController]
	public class ContosoHandshakeController : Controller
	{
		IUserTokenManager _userTokenManager;
		IChatAdminThreadStore _store;
		string _chatGatewayUrl;
		string _resourceConnectionString;

		const string GUID_FOR_INITIAL_TOPIC_NAME = "c774da81-94d5-4652-85c7-6ed0e8dc67e6";

		public ContosoHandshakeController(IChatAdminThreadStore store, IUserTokenManager userTokenManager, IConfiguration chatConfiguration)
		{
			_store = store;
			_userTokenManager = userTokenManager;
			_chatGatewayUrl = Utils.ExtractApiChatGatewayUrl(chatConfiguration["ResourceConnectionString"]);
			_resourceConnectionString = chatConfiguration["ResourceConnectionString"];

			if (!_store.Store.ContainsKey("acs_ve_06_07_2021"))
			{
				var eventInfo = new ACSEvent
				{
					sessionThreadIds = new List<string>() { "19:yXlrdWXkXW8LPAXmDrjcpzg-VwcXz8sE14kWXGez2Qs1@thread.v2" },
					sessionThreadModeratorIds = new List<string>() { "8:acs:85c99b9e-f6e1-408c-90d9-e37b6ad0e7c3_0000000a-8a5c-fc1b-1000-343a0d009850" }
				};
				_store.Store.Add("acs_ve_06_07_2021", JsonSerializer.Serialize(eventInfo));
			}
		}

		/// <summary>
		/// Gets a skype token for the client
		/// </summary>
		/// <returns></returns>
		[Route("token")]
		[HttpPost]
		public async Task<IActionResult> GetTokenAsync()
		{
			var response = await _userTokenManager.GenerateTokenAsync(_resourceConnectionString);

			var clientResponse = new
			{
				user = new {
					communicationUserId = response.User.Id
				},
				token = response.AccessToken.Token,
				expiresOn = response.AccessToken.ExpiresOn
			};

			return this.Ok(clientResponse);
		}

		/// <summary>
		/// Gets a refreshed token for the client
		/// </summary>
		/// <returns></returns>
		[Route("refreshToken/{userIdentity}")]
		[HttpGet]
		public async Task<AccessToken> RefreshTokenAsync(string userIdentity)
		{
			var tokenResponse = await _userTokenManager.RefreshTokenAsync(_resourceConnectionString, userIdentity);
			return  tokenResponse;
		}

		/// <summary>
		/// Get the environment url
		/// </summary>
		/// <returns></returns>
		[Route("getEnvironmentUrl")]
		[HttpGet]
		public string GetEnvironmentUrl()
		{
			return _chatGatewayUrl;
		}

		/// <summary>
		/// Creates a new thread
		/// </summary>
		/// <returns></returns>
		[Route("createThread")]
		[HttpPost]
		public async Task<string> CreateNewThread()
		{
			return await InternalGenerateNewModeratorAndThread();
		}

		/// <summary>
		/// Get event details
		/// </summary>
		/// <returns></returns>
		[Route("event/{eventId}")]
		[HttpGet]
		public ActionResult<ACSEvent> getEventInformation(string eventId)
		{
			if (!_store.Store.ContainsKey(eventId))
			{
				return NotFound();
			}
			return JsonSerializer.Deserialize<ACSEvent>(_store.Store[eventId]);
		}

		/// <summary>
		/// Check if a given thread Id exists in our in memory dictionary
		/// </summary>
		/// <returns></returns>
		[Route("isValidThread/{threadId}")]
		[HttpGet]
		public ActionResult IsValidThread(string threadId)
		{
			if (!_store.Store.ContainsKey(threadId))
			{
				return NotFound();
			}
			return Ok();
		}

		/// <summary>
		/// Add the user to the thread if possible
		/// </summary>
		/// <param name="threadId"></param>
		/// <param name="user"></param>
		/// <returns>200 if successful and </returns>
		[Route("addUser/{threadId}")]
		[HttpPost]
		public async Task<ActionResult> TryAddUserToThread(string threadId, ContosoMemberModel user)
		{
			var eventInfo = JsonSerializer.Deserialize<ACSEvent>(_store.Store["acs_ve_06_07_2021"]);
			var _threadIndex = eventInfo.sessionThreadIds.IndexOf(threadId);
			var moderatorId = eventInfo.sessionThreadModeratorIds[_threadIndex];

			AccessToken moderatorToken = await _userTokenManager.GenerateTokenAsync(_resourceConnectionString, moderatorId);

			ChatClient chatClient = new ChatClient(
				new Uri(_chatGatewayUrl),
				new CommunicationTokenCredential(moderatorToken.Token));

			ChatThreadClient chatThreadClient = chatClient.GetChatThreadClient(threadId);

			var threadProperties = await chatThreadClient.GetPropertiesAsync();
			var chatParticipant = new ChatParticipant(new CommunicationUserIdentifier(user.Id));
			chatParticipant.DisplayName = user.DisplayName;
			chatParticipant.ShareHistoryTime = threadProperties.Value.CreatedOn;

			try
			{
				Response response = await chatThreadClient.AddParticipantAsync(chatParticipant);
				return Ok();
			}
			catch(Exception e)
			{
				Console.WriteLine($"Unexpected error occurred while adding user from thread: {e}");
			}

			return Ok();
		}

		private async Task<string> InternalGenerateNewModeratorAndThread()
		{
			var moderator = await _userTokenManager.GenerateTokenAsync(_resourceConnectionString);

			var moderatorId = moderator.User.Id;
			var moderatorToken = moderator.AccessToken.Token;

			ChatClient chatClient = new ChatClient(
				new Uri(_chatGatewayUrl),
				new CommunicationTokenCredential(moderatorToken));

			List<ChatParticipant> chatParticipants = new List<ChatParticipant>
			{
				new ChatParticipant(new CommunicationUserIdentifier(moderatorId))
			};

			Response<CreateChatThreadResult> result = await chatClient.CreateChatThreadAsync(GUID_FOR_INITIAL_TOPIC_NAME, chatParticipants);

			var threadId = result.Value.ChatThread.Id;

			_store.Store.Add(threadId, moderatorId);
			return threadId;
		}

		/// <summary>
		/// Create a new room
		/// </summary>
		/// <returns>200 if successful and returns roomId</returns>
		[Route("createRoom")]
		[HttpPost]
		public async Task<string> CreateRoom()
		{
				// Our API would generate a new room and return the Id
				return "room_id";
		}

		/// <summary>
		/// Create a thread associated with the room
		/// <param name="roomId"></param>
		/// </summary>
		/// <returns>200 if successful and returns threadId</returns>
		[Route("createRoomThread/{roomId}")]
		[HttpPost]
		public async Task<string> CreateRoomThread(string roomId)
		{
				// Our API would generate a new thread id and associate it with the room
				return "thread_id";
		}

		/// <summary>
		/// Create a thread associated with the room
		/// <param name="roomId"></param>
		/// </summary>
		/// <returns>200 if successful</returns>
		[Route("addUserToRoom/{roomId}")]
		[HttpPost]
		public async void AddUserToRoom(string roomId)
		{
				// Our API would retrieve the associated thread of the room and add user as chat participant
				return;
		}
	}
}