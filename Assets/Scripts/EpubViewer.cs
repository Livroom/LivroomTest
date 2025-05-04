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
    public GameObject coverPage;      // Cover 이미지를 담은 오브젝트
    public GameObject infoPage;       // 제목, 저자 등 정보 표시
    public GameObject contentPage;    // 본문 표시

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

        // Firebase에서 EPUB 다운로드 후 로드
        DownloadAndLoadEpub("eBook/Mobidick.epub");
    }

    // Firebase에서 EPUB 다운로드 후 로드
    private void DownloadAndLoadEpub(string firebasePath)
    {
        // EPUB을 기기 내에 임시로 저장
        string localPath = Path.Combine(Application.persistentDataPath, "Mobidick.epub");
        Debug.Log("파일 저장 경로: " + localPath);

        StorageReference epubRef = storage.GetReference(firebasePath);
        epubRef.GetFileAsync(localPath).ContinueWithOnMainThread(task =>
        {
            if (!task.IsFaulted && !task.IsCanceled)
            {
                Debug.Log("EPUB 다운로드 성공");
                LoadEpub(localPath); // 다운로드 후 EPUB 로드
            }
            else
            {
                Debug.LogError("EPUB 다운로드 실패: " + task.Exception);
            }
        });
    }

    // epub 불러오기
    private async void LoadEpub(string path)
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        EpubBook epubBook = await EpubReader.ReadBookAsync(path);

        // 1. Cover 이미지 처리 (있을 경우)
        if (epubBook.CoverImage != null)
        {
            Texture2D coverTex = new Texture2D(2, 2);
            coverTex.LoadImage(epubBook.CoverImage);
            coverImage.sprite = Sprite.Create(coverTex, new Rect(0, 0, coverTex.width, coverTex.height), new Vector2(0.5f, 0.5f));
        }

        // 2. 메타 정보 페이지
        EpubLocalTextContentFile firstHtml = epubBook.ReadingOrder.FirstOrDefault();
        string metaHtml = firstHtml?.Content;
        if (metaHtml == null)
        {
            Debug.LogWarning("첫 번째 HTML 콘텐츠가 null입니다.");
        }

        Dictionary<string, string> metaInfo = ParseMetaInfoFromHtml(metaHtml);

        // 메타 정보 확인
        string releaseDate = metaInfo.ContainsKey("Release date") ? metaInfo["Release date"] : "Unknown";
        string language = metaInfo.ContainsKey("Language") ? metaInfo["Language"] : "Unknown";
        string credits = metaInfo.ContainsKey("Credits") ? metaInfo["Credits"] : "Unknown";

        Debug.Log($"Release Date: {releaseDate}, Language: {language}, Credits: {credits}");

        // 책 정보 페이지 텍스트 설정
        string infoPageContent = $"<b>{epubBook.Title}</b>\nby {epubBook.Author}\n\n" +
                                 $"<b>Release Date:</b> {releaseDate}\n" +
                                 $"<b>Language:</b> {language}\n" +
                                 $"<b>Credits:</b> {credits}";

        pages.Add(infoPageContent);

        // 3. 본문 페이지 분할
        foreach (EpubLocalTextContentFile chapter in epubBook.ReadingOrder)
        {
            string plainText = StripHtmlTags(chapter.Content);
            List<string> split = SplitTextIntoPages(plainText);
            pages.AddRange(split);
        }

        // 첫 번째 페이지 표시 (커버 페이지)
        ShowPage(0); // 커버 페이지를 표시
    }

    private Dictionary<string, string> ParseMetaInfoFromHtml(string html)
    {
        var doc = new HtmlAgilityPack.HtmlDocument();
        doc.LoadHtml(html);

        Dictionary<string, string> meta = new Dictionary<string, string>();

        // <p> 태그에서 Label: Value 추출
        var pNodes = doc.DocumentNode.SelectNodes("//p");
        if (pNodes != null)
        {
            foreach (var p in pNodes)
            {
                // <strong> 태그 내의 텍스트 추출 (Label)
                var labelNode = p.SelectSingleNode(".//strong");
                if (labelNode != null)
                {
                    string label = labelNode.InnerText.Trim();
                    string value = p.InnerText.Replace(labelNode.InnerText, "").Trim(); // Label을 제외한 나머지 부분이 Value

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

        // 디버깅을 위해 meta 정보 출력
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
                currentPage = line + "\n"; // 새로운 페이지 시작
            }
        }

        if (!string.IsNullOrEmpty(currentPage)) // 마지막 페이지 추가
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

        // 커버 이미지 페이지 (0번째 페이지)
        coverPage.SetActive(index == 0);
        // 정보 페이지 (1번째 페이지)
        infoPage.SetActive(index == 1);
        // 본문 페이지 (2번째 페이지 이후)
        contentPage.SetActive(index >= 2);

        if (index >= 2)
        {
            bookTextUI.text = pages[index]; // 본문 텍스트 설정
        }
        else if (index == 1) // 정보 페이지
        {
            bookTextUI.text = pages[index]; // 정보 페이지 텍스트 설정
        }
    }

    // 이전 페이지 표시
    private void ShowPrevPage()
    {
        if (currentPageIndex > 0)
        {
            ShowPage(currentPageIndex - 1); // 이전 페이지로 이동
        }
    }

    // 다음 페이지 표시
    private void ShowNextPage()
    {
        if (currentPageIndex < pages.Count - 1)
        {
            ShowPage(currentPageIndex + 1); // 다음 페이지로 이동
        }
    }
}
