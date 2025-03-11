namespace Yoda_Bot.Config
{
    public class AppConfig
    {
        // --- Environment Configuration ---
        public long LogsChannelId { get; set; }
        public long StaffChatId { get; set; }
        public string FirebaseDatabaseUrl { get; set; } = ""; //Provide default empty string to avoid null exceptions, will be validated later
        public string FirebaseServiceAccountKeyPath { get; set; } = "";
        public string UserBotToken { get; set; } = "";
        public string StaffBotToken { get; set; } = "";
        public List<long> AdminUserIds { get; set; } = new List<long>();

        // --- Bot Configuration (from JSON) ---
        public string WelcomeMessage { get; set; } = "";
        public string MenuMessage { get; set; } = "";
        public string ServicesMessage { get; set; } = "";
        public string SocialsMessage { get; set; } = "";
        public string TicketCreatedMessage { get; set; } = "";
        public string TicketClosedMessageUser { get; set; } = "";
        public string ContactSupportButton { get; set; } = "";
        public string MenuButton { get; set; } = "";
        public string LanguageButton { get; set; } = "";
        public string EnglishButton { get; set; } = "";
        public string AmharicButton { get; set; } = "";
        public string SelectLanguageMessage { get; set; } = "";
        public string LanguageSetMessage { get; set; } = "";
        public string MessagingOutsideActiveTicket { get; set; } = "";
        public string MessageHistory { get; set; } = "";
        public string StaffReplied { get; set; } = "";
        public string NoMessagesYet { get; set; } = "";
        public string ErrorOccurredMessage { get; set; } = "";
        public string FailedToCreateTicketMessage { get; set; } = "";
        public string FailedToCloseTicketMessage { get; set; } = "";
        public string InvalidSelectionMessage { get; set; } = "";
        public string StaffMenuMessage { get; set; } = "";
        public string ViewTicketsOptionsMessage { get; set; } = "";
        public string NoOpenTicketsMessage { get; set; } = "";
        public string NoClosedTicketsMessage { get; set; } = "";
        public string AllTicketsMessage { get; set; } = "";
        public string OpenTicketsMessage { get; set; } = "";
        public string ClosedTicketsMessage { get; set; } = "";
        public string InvalidTicketTypeMessage { get; set; } = "";
        public string TicketDetailsMessage { get; set; } = "";
        public string TicketIdMessage { get; set; } = "";
        public string UserIdMessage { get; set; } = "";
        public string CreatedMessage { get; set; } = "";
        public string MessageHistoryMessage { get; set; } = "";
        public string NoMessagesYetMessage { get; set; } = "";
        public string ReplyToUserMessage { get; set; } = "";
        public string SwitchTicket { get; set; } = "";
        public string CloseTicketMessage { get; set; } = "";
        public string DeleteTicketMessage { get; set; } = "";
        public string ReopenTicketMessage { get; set; } = "";
        public string CancelButton { get; set; } = "";
        public string ErrorRetrievingTicketDetailsMessage { get; set; } = "";
        public string InvalidTicketNumberSelectedMessage { get; set; } = "";
        public string TelegramApiErrorMessage { get; set; } = "";
        public string ErrorCouldNotFindTicketDataMessage { get; set; } = "";
        public string ErrorCouldNotFindTicketDataForReplyMessage { get; set; } = "";
        public string YouAreNowHandlingTicketMessage { get; set; } = "";
        public string ReplyToUserByTicketIdMessage { get; set; } = "";
        public string TelegramApiErrorInitiatingReplyMessage { get; set; } = "";
        public string ErrorInitiatingReplyToUserMessage { get; set; } = "";
        public string StaffReplyMessageEmptyMessage { get; set; } = "";
        public string InvalidReplyContextMessage { get; set; } = "";
        public string CouldNotParseTicketNumberMessage { get; set; } = "";
        public string ErrorCouldNotFindTicketDataToSendReplyMessage { get; set; } = "";
        public string InvalidTicketNumberInReplyContextMessage { get; set; } = "";
        public string TelegramApiErrorProcessingStaffReplyMessage { get; set; } = "";
        public string ErrorProcessingStaffReplyMessage { get; set; } = "";
        public string ReplySentToUserMessage { get; set; } = "";
        public string TicketClosedMessage { get; set; } = "";
        public string ErrorCannotEditMessageAfterTicketClosureMessage { get; set; } = "";
        public string TelegramApiErrorClosingTicketMessage { get; set; } = "";
        public string ErrorClosingTicketMessage { get; set; } = "";
        public string TicketDeletedMessage { get; set; } = "";
        public string ErrorCannotEditMessageAfterTicketDeletionMessage { get; set; } = "";
        public string TelegramApiErrorTerminatingTicketMessage { get; set; } = "";
        public string ErrorTerminatingTicketMessage { get; set; } = "";
        public string TicketReopenedMessage { get; set; } = "";
        public string ErrorCannotEditMessageAfterTicketReopeningMessage { get; set; } = "";
        public string TelegramApiErrorReopeningTicketMessage { get; set; } = "";
        public string ErrorReopeningTicketMessage { get; set; } = "";
        public string TicketTranscriptSentToLogsMessage { get; set; } = "";
        public string ErrorCouldNotFindTicketDataToGenerateTranscriptMessage { get; set; } = "";
        public string TelegramApiErrorSendingTicketTranscriptMessage { get; set; } = "";
        public string ErrorSendingTicketTranscriptMessage { get; set; } = "";
        public string YouAreNoLongerHandlingTicketMessage { get; set; } = "";
        public string YouAreNotCurrentlyHandlingTicketMessage { get; set; } = "";
        public string SendCloseTicketConfirmationMessage { get; set; } = "";
        public string TelegramApiErrorSendingCloseTicketConfirmationMessage { get; set; } = "";
        public string ErrorSendingCloseTicketConfirmationMessage { get; set; } = "";
        public string SendReopenTicketConfirmationMessage { get; set; } = "";
        public string TelegramApiErrorSendingReopenTicketConfirmationMessage { get; set; } = "";
        public string ErrorSendingReopenTicketConfirmationMessage { get; set; } = "";
        public string SendDeleteTicketConfirmationMessage { get; set; } = "";
        public string TelegramApiErrorSendingDeleteTicketConfirmationMessage { get; set; } = "";
        public string ErrorSendingDeleteTicketConfirmationMessage { get; set; } = "";
        public string SearchTicketsMessage { get; set; } = "";
        public string SearchResultsMessage { get; set; } = "";
        public string NoTicketsFoundMessage { get; set; } = "";
        public string TelegramApiErrorDuringTicketSearchMessage { get; set; } = "";
        public string ErrorDuringTicketSearchMessage { get; set; } = "";
        public string AdminBroadcastMessage { get; set; } = "";
        public string TelegramApiErrorBroadcastingMessage { get; set; } = "";
        public string ErrorBroadcastingMessage { get; set; } = "";
        public string UsageAdminBroadcastMessage { get; set; } = "";
        public string UsageDeleteTicketMessage { get; set; } = "";
        public string UnknownAdminCommandMessage { get; set; } = "";
        public string AvailableAdminCommandsMessage { get; set; } = "";
        public string StaffRepliedToYourTicketMessage { get; set; } = "";
        public string UnsupportedMessageTypeReceivedFromStaffMessage { get; set; } = "";
        public string UserSentMessagePrefix { get; set; } = "";
        public string ImageErrorMessage { get; set; } = "";
        public string AudioErrorMessage { get; set; } = "";
        public string VoiceErrorMessage { get; set; } = "";
        public string VideoErrorMessage { get; set; } = "";
        public string DocumentErrorMessage { get; set; } = "";
        public string UnsupportedMessageTypeMessage { get; set; } = "";
        public string LogTicketCreated { get; set; } = "";
        public string LogTicketClosedByStaff { get; set; } = "";
        public string LogTicketReopenedByStaff { get; set; } = "";
        public string LogTicketDeletedByStaff { get; set; } = "";
        public string LogTicketTranscript { get; set; } = "";
        public string LogTicketDeletedWithTranscript { get; set; } = "";

        // You can add more configuration properties here as needed in the future.
    }
}