using System.Collections.Generic;
using UnityEngine;

public class DataModels
{
    //מחלקת תשובה
    //ניתן להציג באינספקטור
    [System.Serializable] public class AnswerModel
    {
        [Header("Answer Containers")]
        public string textContent;
        public Sprite imageContent;
        public int orderIndex;// משתנה מספרי מאחר שמדובר במשחק סדר וצריך לשמור את המיקום של הפריט
        public bool isImage;
        public bool isRTL;

        public bool IsValid()
        {
            return imageContent != null || !string.IsNullOrWhiteSpace(textContent);
        }
    }
    //מחלקה שאלה
    [System.Serializable]
    public class QuestionModel
    {
        [Header("Question Containers")] public string questionContent;
        public string orderStartLabel;
        public bool isStartLabelRTL;
        public string orderEndLabel;
        public bool isEndLabelRTL;
        public List<AnswerModel> orderedAnswers;
        public int attempts = 0; //לא ניסינו עדיין לענות

    }

    [System.Serializable] public class GameModel
    {
        public string gameName;
        public float timePerQuestion;
        public List<QuestionModel> questionList;
        public bool hasPotions;
        public List<int> awardPotionInds;
    }
}
