from locust import HttpUser, task, between, User, FastHttpUser
import random

# --- Configuration ---
USER_BOT_WEBHOOK_URL = "YOUR_USER_BOT_WEBHOOK_URL_HERE"  # <--- REPLACE THIS!
STAFF_BOT_WEBHOOK_URL = "YOUR_STAFF_BOT_WEBHOOK_URL_HERE" # <--- REPLACE THIS!
EXISTING_TICKET_IDS_FOR_STAFF = ["TCK-000001", "TCK-000002", "TCK-000003"] # <--- REPLACE THESE WITH ACTUAL TICKET IDs!

class YodaUserBotUser(FastHttpUser): # Using FastHttpUser for potentially higher load
    host = "https://fe7d4d7b9eb6d465fdae56a0e8eb7d5c.serveo.net/" # <--- HOST ATTRIBUTE SET HERE!
    wait_time = between(1, 5)  # UserBot users might be less frequent

    def on_start(self):
        # Simulate User Bot user starting - send /start command
        self.client.post("/", json={"update_type": "message", "message": {"text": "/start", "chat": {"id": self.user_id}}})
        self.language_set = False # Flag to track if language is set

    @task(3)
    def create_ticket_and_send_messages(self):
        if not self.language_set:
            # Simulate language selection (if not already set) - you might need to adapt callback data based on your bot
            self.client.post("/", json={"update_type": "callback_query", "callback_query": {"data": "set_language_en", "from": {"id": self.user_id}, "message": {"chat": {"id": self.user_id}}}})
            self.language_set = True # Mark language as set
            return # Stop for this iteration and let wait_time pass before next task

        # Simulate creating a ticket (e.g., clicking "Contact Support" button - adapt callback data if needed)
        response = self.client.post("/", json={"update_type": "message", "message": {"text": "Contact Support", "chat": {"id": self.user_id}}})
        if response.status_code == 200:
            # Extract ticket ID from bot's response (you'll need to parse the text) - VERY BOT SPECIFIC!
            # This is a placeholder - you'll need to implement actual parsing based on your bot's response message
            # ticket_id = extract_ticket_id_from_response(response.text)
            ticket_id = "TCK-PLACEHOLDER" # Placeholder - replace with actual extraction

            # Simulate sending a few messages within the ticket
            num_messages = random.randint(2, 5)
            for _ in range(num_messages):
                message_text = f"User message in ticket {ticket_id}: Need help with issue {random.randint(1, 1000)}"
                self.client.post("/", json={"update_type": "message", "message": {"text": message_text, "chat": {"id": self.user_id}}})

    @task(2)
    def send_menu_command(self):
        self.client.post("/", json={"update_type": "message", "message": {"text": "/menu", "chat": {"id": self.user_id}}})

    @task(1)
    def check_services(self):
         self.client.post("/", json={"update_type": "callback_query", "callback_query": {"data": "check_services", "from": {"id": self.user_id}, "message": {"chat": {"id": self.user_id}}}})

class YodaStaffBotUser(FastHttpUser): # Using FastHttpUser for potentially higher load
    host = "https://fe7d4d7b9eb6d465fdae56a0e8eb7d5c.serveo.net/" # <--- HOST ATTRIBUTE SET HERE!
    wait_time = between(2, 7) # Staff might take longer to respond

    def on_start(self):
        # Simulate Staff Bot user starting - send /start command
        self.client.post("/", json={"update_type": "message", "message": {"text": "/start", "chat": {"id": self.user_id}}})

    @task(3)
    def view_and_handle_ticket(self):
        # Simulate staff viewing open tickets
        self.client.post("/", json={"update_type": "message", "message": {"text": "🎫 View Tickets", "chat": {"id": self.user_id}}})
        self.client.post("/", json={"update_type": "callback_query", "callback_query": {"data": "view_tickets_type-open", "from": {"id": self.user_id}, "message": {"chat": {"id": self.user_id}}}})

        # Simulate staff handling a ticket - select a random ticket ID from pre-existing ones
        ticket_id_to_handle = random.choice(EXISTING_TICKET_IDS_FOR_STAFF) # Use pre-existing Ticket IDs for simplicity
        self.client.post("/", json={"update_type": "callback_query", "callback_query": {"data": f"handle_ticket_by_id-{ticket_id_to_handle}", "from": {"id": self.user_id}, "message": {"chat": {"id": self.user_id}}}})

    @task(2)
    def reply_to_user_in_ticket(self):
        # Simulate replying to a user in a handled ticket (assuming staff is already "handling" a ticket from view_and_handle_ticket)
        ticket_id_reply = random.choice(EXISTING_TICKET_IDS_FOR_STAFF) # Use the same pre-existing Ticket IDs
        reply_text = f"Staff reply to ticket {ticket_id_reply}: We are looking into your issue."
        self.client.post("/", json={"update_type": "message", "message": {"text": reply_text, "chat": {"id": self.user_id}, "reply_to_message": {"text": "💬 Reply to User - Ticket #..."}}}) # Simulate reply context

    @task(1)
    def view_closed_tickets(self):
        self.client.post("/", json={"update_type": "message", "message": {"text": "🎫 View Tickets", "chat": {"id": self.user_id}}})
        self.client.post("/", json={"update_type": "callback_query", "callback_query": {"data": "view_tickets_type-closed", "from": {"id": self.user_id}, "message": {"chat": {"id": self.user_id}}}})