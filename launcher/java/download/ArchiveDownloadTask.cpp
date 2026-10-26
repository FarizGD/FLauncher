// SPDX-License-Identifier: GPL-3.0-only
/*
 *  Prism Launcher - Minecraft Launcher
 *  Copyright (c) 2023-2024 Trial97 <alexandru.tripon97@gmail.com>
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, version 3.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
#include "java/download/ArchiveDownloadTask.h"
#include <memory>

#include <QRegularExpression>

#include "Application.h"
#include "FileSystem.h"
#include "archive/ArchiveReader.h"
#include "archive/ExtractZipTask.h"
#include "net/ChecksumValidator.h"
#include "net/Download.h"
#include "net/NetJob.h"
#include "tasks/Task.h"

namespace Java {
ArchiveDownloadTask::ArchiveDownloadTask(QUrl url, QString final_path, QString checksumType, QString checksumHash)
    : m_url(url), m_final_path(final_path), m_checksum_type(checksumType), m_checksum_hash(checksumHash)
{}

static QString parseChecksumFile(const QByteArray& data)
{
    const auto text = QString::fromUtf8(data);
    static const QRegularExpression re(R"((?<![0-9a-fA-F])([0-9a-fA-F]{64}|[0-9a-fA-F]{40})(?![0-9a-fA-F]))");
    auto match = re.match(text);
    if (!match.hasMatch()) {
        return {};
    }
    return match.captured(1).toLower();
}

void ArchiveDownloadTask::executeTask()
{
    // JRE found ! download the zip
    setStatus(tr("Downloading Java"));

    auto startArchiveDownload = [this](const QString& checksumHash) {
        const auto expectedLength = m_checksum_type == "sha256" ? 64 : m_checksum_type == "sha1" ? 40 : 0;
        if (!m_checksum_type.isEmpty() &&
            (expectedLength == 0 || checksumHash.size() != expectedLength ||
             QByteArray::fromHex(checksumHash.toUtf8()).size() != expectedLength / 2)) {
            emitFailed(tr("The Java download checksum is missing or invalid. Please retry the download."));
            return;
        }
        MetaEntryPtr entry = APPLICATION->metacache()->resolveEntry("java", m_url.fileName());

        auto download = makeShared<NetJob>(QString("JRE::DownloadJava"), APPLICATION->network());
        auto action = Net::Download::makeCached(m_url, entry);
        if (!checksumHash.isEmpty() && !m_checksum_type.isEmpty()) {
            auto hashType = QCryptographicHash::Algorithm::Sha1;
            if (m_checksum_type == "sha256") {
                hashType = QCryptographicHash::Algorithm::Sha256;
            }
            action->addValidator(new Net::ChecksumValidator(hashType, QByteArray::fromHex(checksumHash.toUtf8())));
        }
        download->addNetAction(action);
        auto fullPath = entry->getFullPath();

        connect(download.get(), &Task::failed, this, &ArchiveDownloadTask::emitFailed);
        connect(download.get(), &Task::progress, this, &ArchiveDownloadTask::setProgress);
        connect(download.get(), &Task::stepProgress, this, &ArchiveDownloadTask::propagateStepProgress);
        connect(download.get(), &Task::status, this, &ArchiveDownloadTask::setStatus);
        connect(download.get(), &Task::details, this, &ArchiveDownloadTask::setDetails);
        connect(download.get(), &Task::aborted, this, &ArchiveDownloadTask::emitAborted);
        connect(download.get(), &Task::succeeded, [this, fullPath] {
            // This should do all of the extracting and creating folders
            extractJava(fullPath);
        });
        m_task = download;
        m_task->start();
    };

    if (m_checksum_hash.startsWith("http://") || m_checksum_hash.startsWith("https://")) {
        auto checksumUrl = QUrl(m_checksum_hash);
        MetaEntryPtr checksumEntry = APPLICATION->metacache()->resolveEntry("java", checksumUrl.fileName());
        auto checksumJob = makeShared<NetJob>(QString("JRE::DownloadJavaChecksum"), APPLICATION->network());
        auto checksumAction = Net::Download::makeCached(checksumUrl, checksumEntry);
        checksumJob->addNetAction(checksumAction);

        connect(checksumJob.get(), &Task::failed, this, [this](QString reason) {
            emitFailed(tr("Could not download the Java checksum: %1").arg(reason));
        });
        connect(checksumJob.get(), &Task::succeeded, this, [checksumEntry, startArchiveDownload] {
            auto data = FS::read(checksumEntry->getFullPath());
            auto hash = parseChecksumFile(data);
            startArchiveDownload(hash);
        });
        connect(checksumJob.get(), &Task::progress, this, &ArchiveDownloadTask::setProgress);
        connect(checksumJob.get(), &Task::stepProgress, this, &ArchiveDownloadTask::propagateStepProgress);
        connect(checksumJob.get(), &Task::status, this, &ArchiveDownloadTask::setStatus);
        connect(checksumJob.get(), &Task::details, this, &ArchiveDownloadTask::setDetails);
        connect(checksumJob.get(), &Task::aborted, this, &ArchiveDownloadTask::emitAborted);
        m_task = checksumJob;
        m_task->start();
        return;
    }

    startArchiveDownload(m_checksum_hash);
}


void ArchiveDownloadTask::extractJava(QString input)
{
    setStatus(tr("Extracting Java"));

    MMCZip::ArchiveReader zip(input);
    if (!zip.collectFiles()) {
        emitFailed(tr("Unable to open supplied zip file."));
        return;
    }
    auto files = zip.getFiles();
    if (files.isEmpty()) {
        emitFailed(tr("No files were found in the supplied zip file."));
        return;
    }
    auto firstFolderParts = files[0].split('/', Qt::SkipEmptyParts);
    m_task = makeShared<MMCZip::ExtractZipTask>(input, m_final_path, firstFolderParts.value(0));

    auto progressStep = std::make_shared<TaskStepProgress>();
    connect(m_task.get(), &Task::succeeded, this, [this, progressStep] {
        progressStep->state = TaskStepState::Succeeded;
        stepProgress(*progressStep);
    });

    connect(m_task.get(), &Task::succeeded, this, &ArchiveDownloadTask::emitSucceeded);
    connect(m_task.get(), &Task::aborted, this, &ArchiveDownloadTask::emitAborted);
    connect(m_task.get(), &Task::failed, this, [this, progressStep](QString reason) {
        progressStep->state = TaskStepState::Failed;
        stepProgress(*progressStep);
        emitFailed(reason);
    });
    connect(m_task.get(), &Task::stepProgress, this, &ArchiveDownloadTask::propagateStepProgress);

    connect(m_task.get(), &Task::progress, this, [this, progressStep](qint64 current, qint64 total) {
        progressStep->update(current, total);
        stepProgress(*progressStep);
    });
    connect(m_task.get(), &Task::status, this, [this, progressStep](QString status) {
        progressStep->status = status;
        stepProgress(*progressStep);
    });
    m_task->start();
    return;
}

bool ArchiveDownloadTask::abort()
{
    auto aborted = canAbort();
    if (m_task)
        aborted = m_task->abort();
    return aborted;
};
}  // namespace Java
