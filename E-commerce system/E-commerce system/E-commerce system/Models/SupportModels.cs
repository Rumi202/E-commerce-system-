using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_commerce_system.Models
{
    public class SupportTicket
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string Subject { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        
        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual User User { get; set; }
        
        public string Status { get; set; } // Open, Closed
        public DateTime CreatedAt { get; set; }
        public string? ImageUrl { get; set; }
        
        public virtual ICollection<ChatMessage> Messages { get; set; }
    }

    public class ChatMessage
    {
        [Key]
        public int Id { get; set; }
        
        public int TicketId { get; set; }
        [ForeignKey("TicketId")]
        public virtual SupportTicket Ticket { get; set; }
        
        public int SenderId { get; set; } // Can be User or Admin
        [ForeignKey("SenderId")]
        public virtual User Sender { get; set; }
        
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsAdminReply { get; set; }
    }
}
