"""Small, curated DayZ reference index, bundled with the offline core.

The entries are original summaries, not copies of the linked documentation.
Search is lexical and deterministic; it is not model training.
"""
import re
from dataclasses import asdict, dataclass

BASE = "https://community.bistudio.com/wiki/"
SYNTAX = BASE + "Enscript"
STRUCTURE = BASE + "DayZ:Modding_Structure"
BASICS = BASE + "DayZ:Modding_Basics"
SERVER = BASE + "DayZ:Server_Configuration"


@dataclass(frozen=True)
class Entry:
    id: str
    title: str
    tags: str
    summary: str
    source: str


ENTRIES = (
    Entry("script-types", "Типы, классы и методы Enforce Script", "enforce enscript script класс тип переменная метод функция inheritance array ref managed",
          "DayZ использует Enforce Script, а не C# или C++. Сигнатуры методов и типы нужно сверять с исходниками текущей версии игры. При расширении класса проверяйте тип возвращаемого значения и параметры. Ссылки и жизненный цикл объектов имеют собственные правила языка; не переносите привычки из C# без проверки.", SYNTAX),
    Entry("modded-class", "Расширение через modded class и super", "modded override super наследование патч исправление оригинал pbo метод переопределение addon",
          "Для исправления используйте отдельный мод с modded class существующего класса. Переопределяемый метод должен иметь совместимую сигнатуру. Вызов super сохраняет цепочку предыдущих реализаций, когда это требуется логикой. Несколько модов могут расширять один класс, поэтому важен порядок загрузки. Не изменяйте исходный PBO; если доступного расширения недостаточно, сообщите об ограничении. Пример принципа (вымышленный класс): modded class Example { override void Refresh() { super.Refresh(); } }.", SYNTAX),
    Entry("script-modules", "Слои скриптов и CfgMods", "cfgmods defs files module 1_core 2_gamelib 3_game 4_world 5_mission engineScriptModule gameScriptModule worldScriptModule missionScriptModule модуль скриптов",
          "Скриптовые модули DayZ загружаются последовательно: engine, gamelib, game, world, mission. Для обычных модов часто используются 3_Game, 4_World и 5_Mission. В CfgMods/defs перечислите нужные модули и корректные виртуальные пути files[]. Не создавайте зависимость раннего слоя от класса, доступного только в более позднем.", BASICS),
    Entry("addon-dependencies", "CfgPatches и зависимости аддонов", "cfgpatches requiredaddons dependency зависимость порядок загрузки unknown type missing addon config.cpp патч",
          "В requiredAddons[] указываются реальные идентификаторы CfgPatches требуемых аддонов, а не имена папок @mod и не Workshop ID. Для отдельного патча нужен собственный уникальный класс CfgPatches и зависимость от оригинала. Проверьте имена в доступных исходниках конфигурации. Не угадывайте идентификатор зависимости по названию мода.", BASE + "Class_Inheritance"),
    Entry("mod-layout", "Структура мода и отдельного PBO-патча", "pbo config.cpp mod.cpp addons keys prefix структура папка пакет упаковка патч оригинал",
          "Храните новый мод отдельно от оригинала. Исходный проект содержит config.cpp и скрипты с согласованным префиксом; установленный мод обычно имеет папки addons и keys. mod.cpp описывает представление мода, но не заменяет CfgPatches и CfgMods. Для патча задайте уникальный префикс, версию и перечень зависимостей. Не копируйте чужие ресурсы без разрешения.", STRUCTURE),
    Entry("patch-workflow", "План совместимого исправления без замены исходного мода", "патч исправить фикс fix patch отдельный pbo оригинал конфликт совместимость обновление мод",
          "Правило проекта DayZ AI: изучить точный лог и доступный исходный класс, определить точку расширения, подготовить отдельный addon с минимальным изменением и зависимостью от оригинала, затем проверить на тестовом сервере с той же версией и набором модов. Записать ограничения и способ отключения патча. Не обещать исправление, пока оно не скомпилировано и не проверено. Оригинальные PBO остаются без изменений.", BASICS),
    Entry("compile-diagnostics", "Ошибки компиляции и unknown type", "unknown type cannot compile can't compile undefined function syntax error компиляция ошибка тип функция",
          "Начинайте с первой ошибки компиляции, файла и строки: последующие ошибки могут быть её следствием. Проверьте точное имя класса, сигнатуру, скриптовый слой, наличие нужных PBO и их CfgPatches. Не утверждайте, что причина найдена только по фразе из лога; запросите соответствующие исходники и версию DayZ.", SYNTAX),
    Entry("debugging", "Workbench и проверка скриптов", "workbench debug debugger breakpoint dayzdiag script error null pointer лог стек отладка тест",
          "Для локальной отладки используйте официальный диагностический исполняемый файл DayZ и Workbench. Воспроизведите ошибку на отдельном тестовом экземпляре, изучите стек и состояние объекта; не тестируйте непроверенный патч на рабочем сервере. Проверка синтаксиса текста не заменяет компиляцию и игровой тест.", BASE + "DayZ:Workbench_Script_Debugging"),
    Entry("signatures", "Подписи модов и публичные ключи", "bikey bisign signature подпись ключ ключи pbo verification",
          "В установленном моде публичный ключ используется для проверки подписей. Наличие PBO и bikey само по себе не доказывает корректную подпись. Патч следует подписывать собственным ключом и распространять публичную часть; закрытый ключ не включайте в мод или репозиторий. Не отключайте проверку подписей на рабочем сервере ради обхода ошибок.", STRUCTURE),
    Entry("server-config", "Конфигурация и параметры запуска сервера", "serverdz.cfg server config порт port profiles mission adminlog dologs запуск конфигурация сервер -mod",
          "Путь -config выбирает конфигурацию сервера, -port задаёт игровой порт, -profiles определяет папку профилей и журналов. Пользовательские моды перечисляют в параметре -mod через точку с запятой. До изменения серверных файлов сделайте копию и остановите сервер штатно. Не подставляйте непроверенные настройки или произвольные параметры запуска.", SERVER),
    Entry("admin-logs", "RPT, скриптовые и административные журналы", "rpt adm log logs adminlog журнал лог диагностика подключение",
          "Логи профиля позволяют сопоставить ошибки запуска и скриптов с конкретным моментом и набором модов. ADM включается административным параметром запуска и содержит игровые события; это не замена журналу компиляции. Перед передачей логов удалите пароли и персональные данные игроков.", BASE + "DayZ:Administration_Logs"),
    Entry("economy", "Central Economy и файлы миссии", "types.xml events.xml economy ce loot лут экономика спавн mission xml",
          "Настройки экономики миссии отделены от скриптового кода. Проверяйте синтаксис XML и соответствие классов установленным модам. Изменение цифр в types.xml не гарантирует немедленное появление объектов: важны правила экономики, состояние мира и другие конфигурации. Сохраняйте исходные файлы и тестируйте на отдельной миссии.", BASE + "DayZ:Central_Economy_mission_files_modding"),
    Entry("gameplay", "Настройки игрового процесса JSON", "cfggameplay.json gameplay json настройка миссия config конфигурация",
          "Часть поведения игры настраивается через файл gameplay settings. Проверяйте поддержку параметра в конкретной версии DayZ и включение соответствующей настройки сервера. Валидный JSON не гарантирует корректность полей. Не заменяйте весь существующий файл без сравнения и резервной копии.", BASE + "DayZ:Gameplay_Settings"),
    Entry("steam", "Обновления сервера и Workshop", "steamcmd workshop 223350 221100 обновление скачать установить мод лицензия steam",
          "Для стабильного выделенного сервера используется AppID 223350, а для Workshop DayZ — AppID 221100 и конкретный идентификатор предмета. SteamCMD выполняет авторизацию и загрузку. Перед обновлением остановите сервер и сохраните конфигурации; проверьте совместимость патчей после изменения оригинальных модов.", BASE + "DayZ:Hosting_a_Linux_Server"),
)

STOP = {"как", "что", "это", "для", "при", "или", "the", "and", "for", "with", "dayz", "мод", "мода", "модов"}


def tokens(text: str) -> set[str]:
    return {word for word in re.findall(r"[\w]+", text.lower().replace("ё", "е")) if len(word) >= 2 and word not in STOP}


def match(term: str, words: set[str]) -> bool:
    if term in words:
        return True
    # Limited Russian inflection matching; do not fuzzy-match identifiers.
    return bool(re.fullmatch(r"[а-я]{5,}", term)) and any(
        re.fullmatch(r"[а-я]{5,}", word) and term[:5] == word[:5] for word in words
    )


def search(query: str, limit: int = 5) -> list[dict]:
    terms = tokens(query)
    ranked = []
    for entry in ENTRIES:
        heading, tags, body = tokens(entry.title), tokens(entry.tags), tokens(entry.summary)
        score = sum(4 * match(term, heading) + 3 * match(term, tags) + match(term, body) for term in terms)
        if score:
            ranked.append((score, entry))
    ranked.sort(key=lambda row: (-row[0], row[1].id))
    return [asdict(entry) for _, entry in ranked[:max(1, min(limit, 8))]]


def model_context(entries: list[dict]) -> str:
    return "\n\n".join(f"[{e['id']}] {e['title']}\n{e['summary']}\nИсточник: {e['source']}" for e in entries)
