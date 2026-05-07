using CommunityToolkit.Mvvm.Messaging.Messages;

namespace PersonalExpenseTracker.Messages
{
    public sealed class RefreshExpensesMessage : ValueChangedMessage<bool>
    {
        public RefreshExpensesMessage() : base(true)
        {
        }
    }
}