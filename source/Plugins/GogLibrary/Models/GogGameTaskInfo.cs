﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace GogLibrary.Models
{
    public enum ActionType
    {
        FileTask,
        URLTask
    }

    public class GogGameActionInfo
    {
        public class Task
        {
            public bool isPrimary;
            public ActionType type;
            public string path;
            public string workingDir;
            public string arguments;
            public string link;
            public string name;

            public GameAction ConvertToGenericTask(string installDirectory)
            {
                var action = new GameAction()
                {
                    Arguments = arguments,
                    Name = string.IsNullOrEmpty(name) ? "Play" : name,
                    Path = type == ActionType.FileTask ? Paths.FixSeparators(path) : link,
                    WorkingDir = Paths.FixSeparators(Path.Combine(ExpandableVariables.InstallationDirectory, (workingDir ?? string.Empty))),
                    Type = type == ActionType.FileTask ? GameActionType.File : GameActionType.URL
                };

                // Fixes wrong task confguration generated by some games like Witcher 3
                if (!string.IsNullOrEmpty(workingDir))
                {
                    var fixedWorkDir = Paths.FixSeparators(workingDir);
                    if (action.Path.StartsWith(fixedWorkDir))
                    {
                        action.Path = action.Path.Replace(fixedWorkDir, string.Empty).TrimStart(Path.DirectorySeparatorChar);                        
                    }
                }

                return action;
            }
        }

        public string gameId;
        public string rootGameId;
        public bool standalone;
        public string dependencyGameId;
        public string language;
        public string name;
        public List<Task> playTasks;
        public List<Task> supportTasks;

        public Task DefaultTask
        {
            get
            {
                return playTasks.First(a => a.isPrimary);
            }
        }

    }
}
