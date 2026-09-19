#pragma once

#include <array>
#include <cstddef>

namespace FileToolsMenu
{
// 종류 비트는 처리 대상 집합을 표현한다. Unknown은 중복 제거에 사용하지 않는다.
enum FolderKinds : unsigned { Same = 1, Different = 2, Multiple = 4, Empty = 8, Unknown = 16 };
enum UnwrapCommand : unsigned { SameName = 1, KeepName = 2, UseFolderName = 4, PrefixName = 8, AllContents = 16 };
constexpr unsigned AllUnwrapCommands = 31;

/// <summary>활성 명령에서 적용 대상과 이름/충돌 동작이 같은 후보만 제거한다.</summary>
inline unsigned VisibleUnwrapCommands(unsigned kinds, unsigned enabled, bool singleFileSkipsCollisions)
{
    enabled &= AllUnwrapCommands;
    if (kinds & Unknown)
    {
        return enabled;
    }

    struct Signature { unsigned targets; unsigned naming; unsigned collision; };
    const unsigned singleTargets = kinds & (Same | Different);
    const unsigned singleCollision = singleFileSkipsCollisions ? 0u : 1u;
    // 우선순위: 같은 이름 전용, 파일명 유지, 폴더명, 접두어, 전체 내용.
    const std::array<unsigned, 5> commands{ SameName, KeepName, UseFolderName, PrefixName, AllContents };
    const std::array<Signature, 5> signatures{{
        { kinds & Same, 0, singleCollision },
        { singleTargets, 0, singleCollision },
        { singleTargets, (kinds & Different) ? 1u : 0u, singleCollision },
        { kinds & Different, 2, singleCollision },
        { kinds & (Same | Different | Multiple), 0, 0 }
    }};

    unsigned visible = 0;
    for (std::size_t i = 0; i < commands.size(); ++i)
    {
        if (!(enabled & commands[i]) || signatures[i].targets == 0)
        {
            continue;
        }

        bool duplicate = false;
        for (std::size_t previous = 0; previous < i; ++previous)
        {
            const auto& a = signatures[i];
            const auto& b = signatures[previous];
            if ((visible & commands[previous]) && a.targets == b.targets &&
                a.naming == b.naming && a.collision == b.collision)
            {
                duplicate = true;
                break;
            }
        }

        if (!duplicate)
        {
            visible |= commands[i];
        }
    }
    return visible;
}

// 파일 열거 결과를 주입할 수 있게 분리해 끝/오류를 혼동하지 않는지 검증한다.
enum class EntryKind { End, Error, File, Directory, Ignored };

/// <summary>두 번째 파일 또는 첫 폴더에서 종료한다. 끝까지 읽은 경우만 단일/빈 상태다.</summary>
template<class NextEntry>
unsigned ClassifyFolderEntries(NextEntry next)
{
    bool hasFile = false;
    bool sameName = false;
    for (unsigned reads = 0; reads < 8; ++reads)
    {
        bool entrySameName = false;
        switch (next(entrySameName))
        {
        case EntryKind::End: return hasFile ? (sameName ? Same : Different) : Empty;
        case EntryKind::Error: return Unknown;
        case EntryKind::Directory: return Multiple;
        case EntryKind::File:
            if (hasFile) return Multiple;
            hasFile = true;
            sameName = entrySameName;
            break;
        case EntryKind::Ignored: break;
        }
    }
    return Unknown;
}
}
