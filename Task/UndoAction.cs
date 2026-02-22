using System.Collections.Generic;

namespace TaskApp
{
    public enum UndoActionType
    {
        Complete,
        Delete,
        Create,
        Edit
    }

    public class UndoAction
    {
        public UndoActionType Type { get; set; }
        public string Uid { get; set; } = "";
        public TaskItem? PreviousState { get; set; }
    }
}