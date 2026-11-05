using System;
using System.Text.RegularExpressions;

namespace AbyssMod.Services;

public static class TextClassifier
{
	public const string EquipmentEffect = "equipment_effect";

	public const string AbyssCode = "abyss_code";

	public const string Facility = "facility";

	public const string Bar = "bar";

	public const string Mission = "mission";

	public const string Materials = "materials";

	public const string Dialogue = "dialogue";

	public const string System = "system";

	public const string UiMisc = "ui_misc";

	public const string Name = "name";

	public static readonly string[] AllCustomCategories = new string[9] { "equipment_effect", "abyss_code", "facility", "bar", "mission", "materials", "dialogue", "system", "ui_misc" };

	private static readonly string[] EquipmentKeywords = new string[9] { "紋章：", "スキルチャージ", "会心率", "連撃率", "スキルダメージ", "会心ダメージ", "最大HP", "攻撃力", "防御力" };

	private static readonly string[] AbyssKeywords = new string[12]
	{
		"アビスコード", "浸食率", "フロアキャラ", "フロント全体", "バック全体", "系統", "インパクトコード", "ラッシュコード", "セーフコード", "リスクコード",
		"チェックポイント", "アビスコイン"
	};

	private static readonly string[] FacilityKeywords = new string[14]
	{
		"施設", "酒場の", "研究所", "溶鉱炉", "アップグレード", "建設依頼", "経営戦略", "鍛冶屋", "宿舎", "訓練所",
		"治療所", "噴水", "兵器工場", "鉄甲"
	};

	private static readonly string[] BarKeywords = new string[10] { "スタッフ", "満足度", "の提案", "コスチューム", "おしごと", "ラウンジ", "個室", "VIP席", "に配置", "おにーさん" };

	private static readonly string[] MaterialKeywords = new string[13]
	{
		"純真結晶", "チケット", "ポイント", "コイン", "素材", "鉱石", "骨材", "木材", "マテリアル", "シール",
		"ソウル", "フレンドポイント", "サブスク"
	};

	private static readonly string[] MissionVerbEndings = new string[8] { "する", "しよう", "クリアする", "到達する", "獲得する", "解放する", "アップグレードする", "到達する" };

	private static readonly string[] MissionKeywords = new string[9] { "クリアする", "到達する", "獲得する", "を実行して", "解放する", "にする", "Lvにする", "LvUP", "シンクロLv" };

	private static readonly string[] SystemKeywords = new string[9] { "リトライ", "ソート", "キャンセル", "ショップ", "ゲームを終了", "更新まで", "通信が", "ログインに失敗", "スタミナ" };

	private static readonly string[] DialogueSymbols = new string[5] { "♪", "♡", "～", "……", "…" };

	private static readonly Regex UpRegex = new Regex("\\b(UP|上昇)\\b", RegexOptions.Compiled);

	private static readonly Regex LvRegex = new Regex("Lv\\d", RegexOptions.Compiled);

	public static string Classify(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return "ui_misc";
		}
		if (IsActionSkillDescription(text))
		{
			return "ability_descriptions";
		}
		if (ContainsAny(text, EquipmentKeywords) && (UpRegex.IsMatch(text) || text.Contains("秒間") || text.Contains("自身")))
		{
			return "equipment_effect";
		}
		if (ContainsAny(text, AbyssKeywords))
		{
			return "abyss_code";
		}
		if (ContainsAny(text, FacilityKeywords))
		{
			return "facility";
		}
		if (ContainsAny(text, BarKeywords))
		{
			return "bar";
		}
		if (ContainsAny(text, MaterialKeywords))
		{
			return "materials";
		}
		if (IsMissionText(text))
		{
			return "mission";
		}
		if (IsSystemText(text))
		{
			return "system";
		}
		if (IsDialogue(text))
		{
			return "dialogue";
		}
		return "ui_misc";
	}

	private static bool IsMissionText(string text)
	{
		if (ContainsAny(text, MissionKeywords))
		{
			return true;
		}
		if (LvRegex.IsMatch(text) && (text.Contains("到達") || text.Contains("にする")))
		{
			return true;
		}
		string[] missionVerbEndings = MissionVerbEndings;
		foreach (string value in missionVerbEndings)
		{
			if (text.EndsWith(value, StringComparison.Ordinal))
			{
				return true;
			}
		}
		return false;
	}

	private static bool IsSystemText(string text)
	{
		if (ContainsAny(text, SystemKeywords))
		{
			return true;
		}
		if (text.Length <= 10 && !text.Contains('\n'))
		{
			return true;
		}
		return false;
	}

	private static bool IsDialogue(string text)
	{
		if (!ContainsAny(text, DialogueSymbols))
		{
			return false;
		}
		return text.Length >= 6;
	}

	private static bool ContainsAny(string text, string[] keywords)
	{
		foreach (string value in keywords)
		{
			if (text.Contains(value, StringComparison.Ordinal))
			{
				return true;
			}
		}
		return false;
	}

	public static bool IsActionSkillDescription(string text)
	{
		if (!AbilityTextMatcher.LooksLikeAbility(text))
		{
			return false;
		}
		if (!text.Contains("HIT", StringComparison.OrdinalIgnoreCase) && !text.Contains("ダメージを与", StringComparison.Ordinal) && !text.Contains("攻撃を", StringComparison.Ordinal) && !text.Contains("フォースチェイン", StringComparison.Ordinal))
		{
			return text.Contains("ダメージ倍率", StringComparison.Ordinal);
		}
		return true;
	}
}
