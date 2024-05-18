using System;

namespace Sandbox.EngineLooper
{
    public readonly struct LooperAction
    {
        public readonly object Target;
        public readonly Action Action;

        public LooperAction(object target, Action action)
        {
            if (target is null || action is null)
            {
                throw new ArgumentException("Target and action must not be null.");
            }

            Target = target;
            Action = action;
        }
    }

    public interface IStartable { void Start(); }
    public interface ITickable { void Tick(); }
}