using Shawn.Utils;
using System;

namespace _1RM.Model
{
    public class ProtocolAction : NotifyPropertyChangedBase
    {
        private readonly Func<string> _actionName;
        public string ActionName => _actionName();

        private readonly Action _action;

        public void Run()
        {
            _action?.Invoke();
        }

        public ProtocolAction(string actionName, Action action) : this(() => actionName, action)
        {
        }

        public ProtocolAction(Func<string> actionName, Action action)
        {
            _actionName = actionName;
            _action = action;
        }
    }
}
