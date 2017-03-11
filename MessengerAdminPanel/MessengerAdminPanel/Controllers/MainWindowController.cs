﻿using MessengerAdminPanel.Exceptions;
using MessengerAdminPanel.Factories;
using MessengerAdminPanel.Mapping;
using MessengerAdminPanel.Mapping.EventLogEnums;
using MessengerAdminPanel.Services;
using MessengerAdminPanel.UnitOfWorks;
using MessengerAdminPanel.ViewModels;
using MessengerAdminPanel.Windows;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Windows;
using System.Windows.Threading;

namespace MessengerAdminPanel
{
	public class MainWindowController : IMainWindowController
	{
		private readonly DispatcherTimer _updateDataGridEventLogTimer;
		private readonly DispatcherTimer _updateUserDataTimer;
		private readonly DispatcherTimer _updateUserListViewTimer;

		private readonly IMainWindowView _view;
		private readonly IUnitOfWork _uow;
		private readonly IMappingService _mappingService;
		private readonly IFileService _fileService;

		private Expression<Func<EventLog, bool>> _predicate;
		private int _eventLogEntity;
		private int _eventLogEvent;
		
		private User _user;
		private PersonalInfo _info;

		private string _userId;
		private string _username;
		private int _userData;

		public MainWindowController(IMainWindowView view, IUnitOfWork uof, IMappingService mappingService, IFileService fileService)
		{
			_view = view;
			_uow = uof;
			_mappingService = mappingService;
			_fileService = fileService;

			_updateDataGridEventLogTimer = new DispatcherTimer();
			_updateDataGridEventLogTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);
			_updateDataGridEventLogTimer.Tick += eventLogTimer_Tick;

			_updateUserDataTimer = new DispatcherTimer();
			_updateUserDataTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);
			_updateUserDataTimer.Tick += userDataTimer_Tick;

			_updateUserListViewTimer = new DispatcherTimer();
			_updateUserListViewTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);
			_updateUserListViewTimer.Tick += userListViewTimer_Tick;
		}

		public void UpdateDataGridEventLog(Expression<Func<EventLog, bool>> predicate, int eventLogEntity, int eventLogEvent)
		{
			_updateDataGridEventLogTimer.Stop();
			_updateDataGridEventLogTimer.Start();

			_predicate = predicate;
			_eventLogEntity = eventLogEntity;
			_eventLogEvent = eventLogEvent;
		}

		private void userListViewTimer_Tick(object sender, EventArgs e)
		{
			if (String.IsNullOrEmpty(_userId) && String.IsNullOrEmpty(_username))
			{
				_view.UpdateUserListViewWithUsersList(null);
				return;
			}

			try
			{
				User user;

				if (String.IsNullOrEmpty(_userId))
					user = findUserByUsername(_username);
				else
					user = findUser(_userId);

				switch (_userData)
				{
					case 0:
						var conversations = user.Conversation;
						var mappedConversations = _mappingService.ConversationToViewModel(conversations).ToList();

						_view.UpdateUserListViewWithConversationsList(mappedConversations);
						break;
					case 1:
						var messages = user.Message;
						var mappedMessages = _mappingService.MessageToViewModel(messages).ToList();

						_view.UpdateUserListViewWithMessagesList(mappedMessages);
						break;
					case 2:
						var friends = _uow.UserRepository.FindFriends(user.Id);
						var mappedFriends = _mappingService.UserToViewModel(friends).ToList();

						_view.UpdateUserListViewWithUsersList(mappedFriends);
						break;
				}
			}
			catch (NotFoundException) { _view.UpdateUserListViewWithUsersList(null); }
			catch (ArgumentException) { }
		}

		private void userDataTimer_Tick(object sender, EventArgs e)
		{
			if (_user == null || _info == null)
			{
				_view.UpdateUserData(null, null, null);
				return;
			}

			var picture = _info.Picture;
			var messageVM = _mappingService.UserToViewModel(_user);
			var infoVM = _mappingService.PersonalInfoToViewModel(_info);

			_view.UpdateUserData(messageVM, infoVM, _info.Picture);
		}

		private void eventLogTimer_Tick(object sender, EventArgs e)
		{
			var eventLogList = _uow.EventLogRepositry.FindBy(_predicate);

			if (Enum.IsDefined(typeof(EventLogEntity), _eventLogEntity))
				eventLogList = eventLogList.Where(log => log.EntityId == _eventLogEntity);
			if (Enum.IsDefined(typeof(EventLogEvent), _eventLogEvent))
				eventLogList = eventLogList.Where(log => log.EventId == _eventLogEvent);

			var mappedEventLogList = _mappingService.EventLogToViewModel(eventLogList);
			_view.UpdateDataGridEventLog(mappedEventLogList);
			_updateDataGridEventLogTimer.Stop();
		}

		public void UpdateAnnouncementsListView(bool isActive)
		{
			var announcementsList = _uow.AnnouncementRepository.FindBy(a => a.IsActive == isActive);
			var mappedAnnouncements = _mappingService.AnnouncementToViewModel(announcementsList).ToList();
			_view.UpdateListViewAnnouncement(mappedAnnouncements);
		}

		public void CreateNewAnnouncement()
		{
			var prompt = new PromptWindow("Enter announcement description: ");

			if (!prompt.ShowDialog().Value) return;

			if (String.IsNullOrEmpty(prompt.ResponseText))
			{
				MessageBox.Show("Description cannot be empty!");
				return;
			}

			var announcement = AnnouncementFactory.Create(prompt.ResponseText);
			
			_uow.AnnouncementRepository.Add(announcement);
			_uow.Save();
		}

		public void EditAnnouncement(AnnouncementViewModel a, bool activity)
		{
			if (checkForNullWithErrorMessage(a, "Choose announcement!")) return;

			var prompt = new PromptWindow("Edit announcement description: ", a.Description);
			
			if (!prompt.ShowDialog().Value) return;

			if (String.IsNullOrEmpty(prompt.ResponseText))
			{
				MessageBox.Show("Description cannot be empty!");
				return;
			}

			var newAnnouncement = AnnouncementFactory.Create(prompt.ResponseText);
			var announcement = _uow.AnnouncementRepository.Find(a.Id);

			_uow.AnnouncementRepository.Update(a.Id, announcement, newAnnouncement);
			_uow.Save();

			UpdateAnnouncementsListView(activity);
		}

		public void DeleteAnnouncement(AnnouncementViewModel a, bool activity)
		{
			if (checkForNullWithErrorMessage(a, "Choose announcement!")) return;

			var dialogResult = MessageBox.Show("Are you sure?", "Delete announcement", MessageBoxButton.YesNo);
			if (dialogResult != MessageBoxResult.Yes) return;

			_uow.AnnouncementRepository.Remove(a.Id);
			_uow.Save();

			UpdateAnnouncementsListView(activity);
		}

		public void ChangeAnnouncementStatus(AnnouncementViewModel a, bool activity)
		{
			if (checkForNullWithErrorMessage(a, "Choose announcement!")) return;

			a.IsActive = !a.IsActive;
			
			var announcement = _uow.AnnouncementRepository.Find(a.Id);
			var newAnnouncement = (Announcement)announcement.Clone();
			newAnnouncement.IsActive = !newAnnouncement.IsActive;

			_uow.AnnouncementRepository.Update(a.Id, announcement, newAnnouncement);
			_uow.Save();

			UpdateAnnouncementsListView(activity);
		}

		private bool checkForNullWithErrorMessage(object obj, string nullMsg)
		{
			if (obj != null) return false;

			MessageBox.Show(nullMsg, "Message");
			return true;
		}

		private void fillConversationDataWithEmptiness()
		{
			_view.UpdateConversationData(null);
			_view.UpdateConversationListViewWithUsersList(null);
		}
		
		private Conversation findConversation(string conversationIdStr)
		{
			int conversationId;
			if (!int.TryParse(conversationIdStr, out conversationId))
			{
				fillConversationDataWithEmptiness();
				throw new ArgumentException($"{conversationIdStr} cannot be resolved as conversation id.");
			}

			var conversation = _uow.ConversationRepository.Find(conversationId);
			if (conversation == null)
			{
				fillConversationDataWithEmptiness();
				throw new NotFoundException($"Conversation with id {conversationId} not found.");
			}

			return conversation;
		}

		public void UpdateConversationData(string conversationIdStr)
		{
			try
			{
				var conversation = findConversation(conversationIdStr);
				var conversationVM = _mappingService.ConversationToViewModel(conversation);

				_view.UpdateConversationData(conversationVM);
			}
			catch (NotFoundException) { }
			catch (ArgumentException) { }
		}

		public void UpdateListViewUsersInConversation(string conversationIdStr)
		{
			try
			{
				var conversation = findConversation(conversationIdStr);
				var users = conversation.User.ToList();
				var mappedUsers = _mappingService.UserToViewModel(users).ToList();
				
				_view.UpdateConversationListViewWithUsersList(mappedUsers);
			}
			catch (NotFoundException) { }
			catch (ArgumentException) { }
		}

		public void UpdateListViewMessagesInConversation(string conversationIdStr)
		{
			try
			{
				var conversation = findConversation(conversationIdStr);
				var messages = conversation.Message.ToList();
				var mappedMessages = _mappingService.MessageToViewModel(messages).ToList();
		
				_view.UpdateConversationListViewWithMessagesList(mappedMessages);
			}
			catch (NotFoundException) { }
			catch (ArgumentException) { }
		}

		public void UpdateMessageData(string messageId)
		{
			int id;
			if (!int.TryParse(messageId, out id))
			{
				_view.UpdateMessageData(null, null);
				return;
			}
		
			var message = _uow.MessageRepository.Find(id);
			if (message == null)
			{
				_view.UpdateMessageData(null, null);
				return;
			}

			var messageVM = _mappingService.MessageToViewModel(message);
			_view.UpdateMessageData(messageVM, message.Attachment);
		}

		public void OpenFile(string fileName)
		{
			try
			{
				_fileService.OpenFile(fileName);
			} 
			catch (Exception ex) when (ex is Win32Exception || ex is FileNotFoundException)
			{
				MessageBox.Show("File not found.", "Error");
			}
		}

		private void updateUserData(User user, PersonalInfo info)
		{
			_user = user;
			_info = info;

			_updateUserDataTimer.Stop();
			_updateUserDataTimer.Start();
		}

		public void UpdateUserDataById(string userId)
		{
			int id;
			if (!int.TryParse(userId, out id))
			{
				updateUserData(null, null);
				return;
			}

			var user = _uow.UserRepository.Find(id);
			var info = _uow.PersonalInfoRepository.Find(id);

			updateUserData(user, info);
		}

		private User findUserByUsername(string username)
		{
			username = username.ToLower();

			var userEnumerable = _uow.UserRepository.FindBy(u => u.Login.Equals(username.ToLower()));

			if (userEnumerable.Count() == 0)
				throw new NotFoundException($"User with login [{username}] not found.");

			return userEnumerable.First();
		}

		public void UpdateUserDataByUsername(string username)
		{
			try
			{
				var user = findUserByUsername(username);
				var info = _uow.PersonalInfoRepository.Find(user.Id);

				updateUserData(user, info);
			}
			catch (NotFoundException) { updateUserData(null, null); }
		}

		public void ChangeUsername(string currentUsername)
		{
			var prompt = new PromptWindow("Username:", currentUsername);

			if (!prompt.ShowDialog().Value) return;

			if (String.IsNullOrEmpty(prompt.ResponseText))
			{
				MessageBox.Show("Username cannount by empty.");
				return;
			}

			if (prompt.ResponseText.Length < 5)
			{
				MessageBox.Show("Username length cannot be less than 5 characters.");
				return;
			}

			try
			{
				var user = findUserByUsername(currentUsername);
				user.Login = prompt.ResponseText;
				_uow.Save();

				var info = _uow.PersonalInfoRepository.Find(user.Id);

				updateUserData(user, info);
			}
			catch (NotFoundException) { }
		}

		public void ChangeUserBanStatus(string username, bool status)
		{
			var user = findUserByUsername(username);
			user.IsBanned = status;
			_uow.Save();
		}

		public void ChangeUserAdminStatus(string username, bool status)
		{
			var user = findUserByUsername(username);
			user.IsAdmin = status;
			_uow.Save();
		}

		public void ChangeConversationName(string conversaitonId, string currentName)
		{
			var id = Int32.Parse(conversaitonId);
			var conversation = _uow.ConversationRepository.Find(id);
			var prompt = new PromptWindow("Conversation name:", currentName);

			if (!prompt.ShowDialog().Value) return;

			if (String.IsNullOrEmpty(prompt.ResponseText))
			{
				MessageBox.Show("Username cannount by empty.");
				return;
			}

			conversation.Name = prompt.ResponseText;
			_uow.Save();

			var conversationVM = _mappingService.ConversationToViewModel(conversation);
			_view.UpdateConversationData(conversationVM);
		}

		public void DeleteConversation(string conversationId)
		{
			var dialogResult = MessageBox.Show("Are you sure?", "Delete conversation", MessageBoxButton.YesNo);
			if (dialogResult != MessageBoxResult.Yes) return;

			var id = Int32.Parse(conversationId);
			_uow.ConversationRepository.Remove(id);
			_uow.Save();

			_view.UpdateConversationData(null);
		}

		public void DeleteMessage(string messageId)
		{
			var dialogResult = MessageBox.Show("Are you sure?", "Delete message", MessageBoxButton.YesNo);
			if (dialogResult != MessageBoxResult.Yes) return;

			var id = Int32.Parse(messageId);
			_uow.MessageRepository.Remove(id);
			_uow.Save();

			_view.UpdateMessageData(null, String.Empty);
		}

		private User findUser(string userIdStr)
		{
			int userId;
			if (!int.TryParse(userIdStr, out userId))
			{
				fillConversationDataWithEmptiness();
				throw new ArgumentException($"{userIdStr} cannot be resolved as user id.");
			}

			var conversation = _uow.UserRepository.Find(userId);
			if (conversation == null)
			{
				fillConversationDataWithEmptiness();
				throw new NotFoundException($"User with id {userId} not found.");
			}

			return conversation;
		}

		public void UpdateUserListView(string userId, string username, int userData)
		{
			_userId = userId;
			_username = username;
			_userData = userData;

			_updateUserListViewTimer.Stop();
			_updateUserListViewTimer.Start();
		}
	}
}