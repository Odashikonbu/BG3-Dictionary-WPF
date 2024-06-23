using LiteDB;
using Microsoft.Win32;
using System.DirectoryServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;

namespace BG3_Dictionary_WPF
{   /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private struct TranslationData
        {
            public ObjectId ID { get; set; }
            public string UUID { get; set; }
            public string SourceLang { get; set; }
            public string TransLang { get; set; }
        }
        private string DBPath = "translation.db";
        private List<TranslationData> result;
        private Boolean SortedUUID = true;
        private Boolean SortedSource = false;
        private Boolean SortedTrans = false;
        public MainWindow()
        {
            InitializeComponent();

            //result初期化
            result = null;
            //DataGridView幅設定
            SetupDataGridView();
        }
        private void Top_Loaded(object sender, RoutedEventArgs e)
        {
            using (var db = new LiteDatabase(DBPath))
            {
                var collection = db.GetCollection<TranslationData>("TranslationData");
                var data = collection.Find(Query.All(), limit: 10).ToList();
                if (data.Count < 10)
                {
                    SearchBox.IsEnabled = false;
                    SearchButton.IsEnabled = false;
                }

            }
        }
        private void SetupDataGridView()
        {
            SearchResult.AutoGenerateColumns = false;
            SearchResult.Columns.Clear();



            // UUID
            DataGridTextColumn uuidColumn = new DataGridTextColumn
            {
                Binding = new Binding("UUID"),
                Header = "uuid",
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            };
            SearchResult.Columns.Add(uuidColumn);

            // 原文
            DataGridTextColumn sourceLangColumn = new DataGridTextColumn
            {
                Binding = new Binding("SourceLang"),
                Header = "Source",
                Width = new DataGridLength(3, DataGridLengthUnitType.Star)
            };
            SearchResult.Columns.Add(sourceLangColumn);


            // 訳文
            DataGridTextColumn transLangColumn = new DataGridTextColumn
            {
                Binding = new Binding("TransLang"),
                Header = "Translated",
                Width = new DataGridLength(6, DataGridLengthUnitType.Star)
            };
            SearchResult.Columns.Add(transLangColumn);

            // カラム幅を自動調整
            SearchResult.Loaded += (s, e) =>
            {
                SearchResult.Columns[0].Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                SearchResult.Columns[1].Width = new DataGridLength(3, DataGridLengthUnitType.Star);
                SearchResult.Columns[2].Width = new DataGridLength(6, DataGridLengthUnitType.Star);
            };

        }

        private void CreateDictionaryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Filter = "XML files (*.xml)|*.xml",
                    Title = "英語XMLファイルを選択してください"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    string sourceXML = openFileDialog.FileName;

                    openFileDialog.Reset();
                    openFileDialog.Title = "日本語XMLファイルを選択してください";

                    if (openFileDialog.ShowDialog() == true)
                    {
                        string transXML = openFileDialog.FileName;

                        // XML読み込み
                        XDocument sourceDoc = XDocument.Load(sourceXML);
                        XDocument translationDoc = XDocument.Load(transXML);

                        // XML辞書化
                        var sourceSentences = sourceDoc.Descendants("content")
                            .Where(x => x.Attribute("contentuid") != null)
                            .Select(x => new
                            {
                                Key = (string)x.Attribute("contentuid") ?? "",
                                Value = (string)x ?? ""
                            })
                            .ToDictionary(x => x.Key, x => x.Value);
                        var translationSentences = translationDoc.Descendants("content")
                            .Where(x => x.Attribute("contentuid") != null)
                            .Select(x => new
                            {
                                Key = (string)x.Attribute("contentuid") ?? "",
                                Value = (string)x ?? ""
                            })
                            .ToDictionary(x => x.Key, x => x.Value);

                        // 比較翻訳結果を格納
                        List<TranslationData> langDatasets = new List<TranslationData>();

                        // uuidをキーに対訳
                        foreach (var uid in sourceSentences.Keys)
                        {
                            if (translationSentences.ContainsKey(uid))
                            {
                                string sourceWord = sourceSentences[uid];
                                string transWord = translationSentences[uid];

                                langDatasets.Add(new TranslationData
                                {
                                    UUID = uid,
                                    SourceLang = sourceWord,
                                    TransLang = transWord
                                });
                            }
                        }
                        InsertDB(DBPath, langDatasets);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"データのインポート中にエラーが発生しました。\n\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void InsertDB(string dbPath, List<TranslationData> list)
        {
            using (var db = new LiteDatabase(dbPath))
            {
                var collection = db.GetCollection<TranslationData>("TranslationData");
                collection.DeleteAll();

                // DB構築
                collection.InsertBulk(list);
                MessageBox.Show($"Creating Dictionary Complete", "Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                //最低10件以上あれば検索ボタン活性化
                var data = collection.Find(Query.All(), limit: 10).ToList();
                if (data.Count > 0)
                {
                    SearchButton.IsEnabled = true;
                    SearchBox.IsEnabled = true;
                }
            }
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            string searchText = SearchBox.Text.Trim();
            if (!string.IsNullOrEmpty(searchText))
            {
                SearchBox.IsEnabled = false;
                SearchButton.IsEnabled = false;
                SearchBox.Opacity = 0.5;
                using (var db = new LiteDatabase(DBPath))
                {
                    var collection = db.GetCollection<TranslationData>("TranslationData");
                    // インデックス付ける
                    collection.EnsureIndex(x => x.ID);
                    result = collection.Find(x => x.SourceLang.Contains(searchText)).Select(x => new TranslationData
                    {
                        UUID = x.UUID,
                        SourceLang = x.SourceLang,
                        TransLang = x.TransLang
                    }).ToList(); ;

                    if (result.Count > 0)
                    {
                        SetupDataGridView();
                        SearchResult.ItemsSource = result;
                        SearchResult.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        SearchResult.Visibility = Visibility.Hidden;
                    }

                }
                SearchBox.IsEnabled = true;
                SearchBox.Opacity = 1;
                SearchButton.IsEnabled = true;
            }

        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SearchButton_Click(sender, e);
                e.Handled = true;
            }
        }
    }
}