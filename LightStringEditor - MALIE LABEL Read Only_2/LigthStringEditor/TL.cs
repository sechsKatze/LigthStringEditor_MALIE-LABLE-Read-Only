using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LigthStringEditor {
    public class DatTL {

        LightDat Editor;
        public DatTL(byte[] Script) { Editor = new LightDat(Script); }

        // ✅ MalieLabelCount 프로퍼티 추가
        public int MalieLabelCount => Editor.MalieLabelCount;
        
        // ✅ 필터링된 MALIE LABEL 개수 (GUI에서 사용)
        public int FilteredMalieLabelCount => filteredToOriginalMap.Count;

        // ✅ 필터링 인덱스 매핑 (UI index → 원본 index)
        private List<int> filteredToOriginalMap = new List<int>();
        
        // ✅ 필터 활성화 여부
        public bool FilterEnabled { get; set; } = false;

        private Dictionary<SplitInf, string> Cutted;
        private Dictionary<uint, uint> SplitMap;

        private Dictionary<uint, string> Prefix;
        private Dictionary<uint, string> Sufix;

        private uint StrCount;
        private string[] MalieLabels;  // ✅ MALIE LABEL 저장
        
        public string[] Import() {
            SplitMap = new Dictionary<uint, uint>();
            Cutted = new Dictionary<SplitInf, string>();
            Prefix = new Dictionary<uint, string>();
            Sufix = new Dictionary<uint, string>();

            string[] AllStrings = Editor.Import();
            
            // ✅ MALIE LABEL 분리 (split 처리 안 함)
            int malieLabelCount = Editor.MalieLabelCount;
            MalieLabels = AllStrings.Take(malieLabelCount).ToArray();
            string[] Strings = AllStrings.Skip(malieLabelCount).ToArray();
            
            StrCount = (uint)Strings.LongLength;
            List<char> DenyChars = new List<char>(new char[] { '\t', '\n', '\r', '\a', '\b', '\0' });

            // ✅ 필터링 로직
            filteredToOriginalMap.Clear();
            List<string> SplitedStrings = new List<string>();
            
            if (FilterEnabled) {
                // 필터 활성화: 중요한 MALIE_LABEL만 추가
                for (int i = 0; i < MalieLabels.Length; i++) {
                    if (IsImportantLabel(MalieLabels[i])) {
                        filteredToOriginalMap.Add(i);
                        SplitedStrings.Add(MalieLabels[i]);
                    }
                }
            } else {
                // 필터 비활성화: 모든 MALIE_LABEL 추가
                for (int i = 0; i < MalieLabels.Length; i++) {
                    filteredToOriginalMap.Add(i);
                    SplitedStrings.Add(MalieLabels[i]);
                }
            }
            
            // ✅ STRING TABLE만 split 처리 (필터링 안 함)
            for (uint x = 0; x < Strings.LongLength; x++) {
                string String = Strings[x];
                Clear(ref String, x, DenyChars);

                List<string> Split = new List<string>();

                int Start = 0;
                for (int i = 0; i < String.Length; i++) {
                    string CutContent = string.Empty;
                    // ✅ Index Out of Bounds 방지
                    while (i < String.Length && (DenyChars.Contains(String[i]) || String[i] < 32)) {
                        CutContent += String[i++];
                    }

                    if (CutContent != string.Empty) {
                        SplitInf Inf = new SplitInf();
                        Inf.Split = Split.Count;
                        Inf.String = x;
                        Cutted.Add(Inf, CutContent);

                        Split.Add(String.Substring(Start, i - CutContent.Length - Start));
                        Start = i;
                    }
                }

                Split.Add(String.Substring(Start, String.Length - Start));

                SplitMap.Add(x, (uint)Split.LongCount());
                SplitedStrings.AddRange(Split);
            }

            return SplitedStrings.ToArray();
        }
        
        // ✅ 캐릭터명/선택지만 필터링 (챕터명 제외)
        private bool IsImportantLabel(string label) {
            // 빈 문자열 제외
            if (string.IsNullOrWhiteSpace(label))
                return false;
            
            // ❌ 블랙리스트: 시스템 라벨 제외
            if (label.StartsWith("malie_") || 
                label.StartsWith("■ [LABEL]") ||
                label.StartsWith("v_") ||
                label.StartsWith("wait") ||
                label.StartsWith("time"))
                return false;
            
            // ✅ 챕터명은 특별히 허용!
            if (label.Contains("<chapter name="))
                return true;
            
            // ❌ 다른 XML 태그는 제외
            if (label.StartsWith("<"))
                return false;
            
            // ❌ 소수점 숫자만 있는 경우 제외 (0.1, 0.5s 등)
            if (label.Contains("0.") && label.Replace("0.", "").Replace("s", "").Replace(".", "").All(char.IsDigit))
                return false;
            
            // ✅ 화이트리스트: 캐릭터명/선택지/챕터명
            
            // 1. 길이 제한 (1~100자)
            if (label.Length < 1 || label.Length > 100)
                return false;
            
            // 2. 출력 가능한 문자가 있는지 확인
            bool hasPrintableChar = label.Any(c => {
                int code = (int)c;
                return code >= 32 ||                           // ASCII 출력 가능
                       (code >= 0xFF00 && code <= 0xFFEF) ||  // 전각 문자
                       (code >= 0x3040 && code <= 0x30FF) ||  // 히라가나/카타카나
                       (code >= 0x4E00 && code <= 0x9FFF) ||  // 한자
                       (code >= 0xAC00 && code <= 0xD7AF);    // 한글
            });
            
            if (!hasPrintableChar)
                return false;
            
            // ✅ 최종 통과: 캐릭터명, 선택지, 챕터명
            return true;
        }

        private void Clear(ref string String, uint ID, List<char> DenyChars) {
            string Prefix = string.Empty;
            while (String.Length > 0 && (DenyChars.Contains(String[0]) || String[0] < 32)) {
                Prefix += String[0];
                String = String.Substring(1, String.Length - 1);
            }

            string Sufix = string.Empty;
            while (String.Length > 0 && (DenyChars.Contains(String[String.Length - 1]) || String[String.Length - 1] < 32)) {
                Sufix = String[String.Length - 1] + Sufix;
                String = String.Substring(0, String.Length - 1);
            }

            this.Prefix.Add(ID, Prefix);
            this.Sufix.Add(ID, Sufix);
        }

        public byte[] Export(string[] Strings) {
            // MALIE LABEL - 원본 그대로 사용 (편집 사항 무시)
            int filteredMalieLabelCount = filteredToOriginalMap.Count;
            string[] exportMalieLabels = MalieLabels;  // 원본 그대로
            
            // STRING TABLE 분리
            string[] exportStrings = Strings.Skip(filteredMalieLabelCount).ToArray();
            
            // ✅ STRING TABLE만 merge
            List<string> MergedStrings = new List<string>();
            for (uint x = 0, z = 0; x < StrCount; x++) {
                string Str = string.Empty;
                for (int i = 0; i < SplitMap[x]; i++) {
                    SplitInf Inf = new SplitInf();
                    Inf.Split = i;
                    Inf.String = x;

                    Str += exportStrings[z++];
                    if (i < SplitMap[x] - 1)
                        Str += Cutted[Inf];
                }

                MergedStrings.Add(Prefix[x] + Str + Sufix[x]);
            }

            if (StrCount != MergedStrings.LongCount())
                throw new Exception("Failed to Merge Strings.");

            // ✅ MALIE LABEL + STRING TABLE 합치기
            List<string> FinalStrings = new List<string>(exportMalieLabels);
            FinalStrings.AddRange(MergedStrings);

            return Editor.Export(FinalStrings.ToArray());
        }
        
        internal struct SplitInf {
            internal uint String;
            internal int Split;
        }
    }
}