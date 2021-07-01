using System.Collections.Generic;

namespace Kavita.Common.Update
{
    public class UpdateChanges
    {
        public List<string> New { get; set; }
        public List<string> Fixed { get; set; }
        public List<string> Changed { get; set; }

        public UpdateChanges()
        {
            New = new List<string>();
            Fixed = new List<string>();
            Changed = new List<string>();
        }
    }
}