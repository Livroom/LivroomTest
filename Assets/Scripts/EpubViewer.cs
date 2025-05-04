using Firebase.Extensions;
using Firebase.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VersOne.Epub;

public class EpubViewer : MonoBehaviour
{
    public Image coverImage;
    public TMP_Text bookTextUI;
    public GameObject coverPage;      // Cover �̹����� ���� ������Ʈ
    public GameObject infoPage;       // ����, ���� �� ���� ǥ��
    public GameObject contentPage;    // ���� ǥ��

    public Button prevButton;
    public Button nextButton;

    private List<string> pages = new List<string>();
    private int currentPageIndex = 0;

    private FirebaseStorage storage;

    private void Start()
    {
        storage = FirebaseStorage.DefaultInstance;

        prevButton.onClick.AddListener(ShowPrevPage);
        nextButton.onClick.AddListener(ShowNextPage);

        // Firebase���� EPUB �ٿ�ε� �� �ε�
        DownloadAndLoadEpub("eBook/Mobidick.epub");
    }

    // Firebase���� EPUB �ٿ�ε� �� �ε�
    private void DownloadAndLoadEpub(string firebasePath)
    {
        // EPUB�� ��� ���� �ӽ÷� ����
        string localPath = Path.Combine(Application.persistentDataPath, "Mobidick.epub");
        Debug.Log("���� ���� ���: " + localPath);

        StorageReference epubRef = storage.GetReference(firebasePath);
        epubRef.GetFileAsync(localPath).ContinueWithOnMainThread(task =>
        {
            if (!task.IsFaulted && !task.IsCanceled)
            {
                Debug.Log("EPUB �ٿ�ε� ����");
                LoadEpub(localPath); // �ٿ�ε� �� EPUB �ε�
            }
            else
            {
                Debug.LogError("EPUB �ٿ�ε� ����: " + task.Exception);
            }
        });
    }

    // epub �ҷ�����
    private async void LoadEpub(string path)
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        EpubBook epubBook = await EpubReader.ReadBookAsync(path);

        // 1. Cover �̹��� ó�� (���� ���)
        if (epubBook.CoverImage != null)
        {
            Texture2D coverTex = new Texture2D(2, 2);
            coverTex.LoadImage(epubBook.CoverImage);
            coverImage.sprite = Sprite.Create(coverTex, new Rect(0, 0, coverTex.width, coverTex.height), new Vector2(0.5f, 0.5f));
        }

        // 2. ��Ÿ ���� ������
        EpubLocalTextContentFile firstHtml = epubBook.ReadingOrder.FirstOrDefault();
        string metaHtml = firstHtml?.Content;
        if (metaHtml == null)
        {
            Debug.LogWarning("ù ��° HTML �������� null�Դϴ�.");
        }

        Dictionary<string, string> metaInfo = ParseMetaInfoFromHtml(metaHtml);

        // ��Ÿ ���� Ȯ��
        string releaseDate = metaInfo.ContainsKey("Release date") ? metaInfo["Release date"] : "Unknown";
        string language = metaInfo.ContainsKey("Language") ? metaInfo["Language"] : "Unknown";
        string credits = metaInfo.ContainsKey("Credits") ? metaInfo["Credits"] : "Unknown";

        Debug.Log($"Release Date: {releaseDate}, Language: {language}, Credits: {credits}");

        // å ���� ������ �ؽ�Ʈ ����
        string infoPageContent = $"<b>{epubBook.Title}</b>\nby {epubBook.Author}\n\n" +
                                 $"<b>Release Date:</b> {releaseDate}\n" +
                                 $"<b>Language:</b> {language}\n" +
                                 $"<b>Credits:</b> {credits}";

        pages.Add(infoPageContent);

        // 3. ���� ������ ����
        foreach (EpubLocalTextContentFile chapter in epubBook.ReadingOrder)
        {
            string plainText = StripHtmlTags(chapter.Content);
            List<string> split = SplitTextIntoPages(plainText);
            pages.AddRange(split);
        }

        // ù ��° ������ ǥ�� (Ŀ�� ������)
        ShowPage(0); // Ŀ�� �������� ǥ��
    }

    private Dictionary<string, string> ParseMetaInfoFromHtml(string html)
    {
        var doc = new HtmlAgilityPack.HtmlDocument();
        doc.LoadHtml(html);

        Dictionary<string, string> meta = new Dictionary<string, string>();

        // <p> �±׿��� Label: Value ����
        var pNodes = doc.DocumentNode.SelectNodes("//p");
        if (pNodes != null)
        {
            foreach (var p in pNodes)
            {
                // <strong> �±� ���� �ؽ�Ʈ ���� (Label)
                var labelNode = p.SelectSingleNode(".//strong");
                if (labelNode != null)
                {
                    string label = labelNode.InnerText.Trim();
                    string value = p.InnerText.Replace(labelNode.InnerText, "").Trim(); // Label�� ������ ������ �κ��� Value

                    if (!string.IsNullOrEmpty(label) && !string.IsNullOrEmpty(value))
                    {
                        meta[label] = value;
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning("No <p> tags found in the HTML.");
        }

        // ������� ���� meta ���� ���
        foreach (var item in meta)
        {
            Debug.Log($"Meta Info: {item.Key} = {item.Value}");
        }

        return meta;
    }

    private List<string> SplitTextIntoPages(string fullText, int maxLinesPerPage = 20)
    {
        List<string> pageList = new List<string>();
        string currentPage = "";
        string[] lines = fullText.Split(new[] { '\n' }, StringSplitOptions.None);

        foreach (string line in lines)
        {
            if (currentPage.Split('\n').Length < maxLinesPerPage)
            {
                currentPage += line + "\n";
            }
            else
            {
                pageList.Add(currentPage);
                currentPage = line + "\n"; // ���ο� ������ ����
            }
        }

        if (!string.IsNullOrEmpty(currentPage)) // ������ ������ �߰�
        {
            pageList.Add(currentPage);
        }

        return pageList;
    }

    private string StripHtmlTags(string html)
    {
        var doc = new HtmlAgilityPack.HtmlDocument();
        doc.LoadHtml(html);
        return doc.DocumentNode.InnerText;
    }

    private void ShowPage(int index)
    {
        currentPageIndex = index;

        // Ŀ�� �̹��� ������ (0��° ������)
        coverPage.SetActive(index == 0);
        // ���� ������ (1��° ������)
        infoPage.SetActive(index == 1);
        // ���� ������ (2��° ������ ����)
        contentPage.SetActive(index >= 2);

        if (index >= 2)
        {
            bookTextUI.text = pages[index]; // ���� �ؽ�Ʈ ����
        }
        else if (index == 1) // ���� ������
        {
            bookTextUI.text = pages[index]; // ���� ������ �ؽ�Ʈ ����
        }
    }

    // ���� ������ ǥ��
    private void ShowPrevPage()
    {
        if (currentPageIndex > 0)
        {
            ShowPage(currentPageIndex - 1); // ���� �������� �̵�
        }
    }

    // ���� ������ ǥ��
    private void ShowNextPage()
    {
        if (currentPageIndex < pages.Count - 1)
        {
            ShowPage(currentPageIndex + 1); // ���� �������� �̵�
        }
    }
}
