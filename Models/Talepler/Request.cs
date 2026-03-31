using System;
using System.Collections.Generic;

namespace SOS.Models.Talepler
{
    public class Request
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CustomerId { get; set; } = string.Empty;
        public string ProjectId { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public string Status { get; set; } = "Analiz";
        public string DevOpsStatus { get; set; } = string.Empty;
        public string Date { get; set; } = DateTime.Now.ToString("dd.MM.yyyy");
        public string? LastModifiedDate { get; set; }
        public string? PlanlananPyuat { get; set; }
        public string? GerceklesenPyuat { get; set; }
        public string? PlanlananCanliTeslim { get; set; }
        public string? GerceklesenCanliTeslim { get; set; }
        public string Priority { get; set; } = "Orta";
        public int Progress { get; set; } = 0;
        public string Budget { get; set; } = "-";
        public string Effort { get; set; } = "-";
        public string Cost { get; set; } = "-";
        public string? VarunaKisiGun { get; set; }
        public string? Po { get; set; }
        public string Type { get; set; } = "Geliştirme";
        public string AssignedTo { get; set; } = string.Empty;
        public string? LastRevisionNote { get; set; }
        public List<Subtask> Subtasks { get; set; } = new();
        public List<Comment> Comments { get; set; } = new();
        public List<HistoryItem> History { get; set; } = new();
        public int? SurveyScore { get; set; }
        public string? SurveyNote { get; set; }
    }

    public class Subtask
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public bool Completed { get; set; } = false;
    }

    public class Comment
    {
        public string Id { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
    }

    public class HistoryItem
    {
        public string Id { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }
}

