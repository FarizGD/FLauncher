// SPDX-License-Identifier: GPL-3.0-only
/*
 *  Prism Launcher - Minecraft Launcher
 *  Copyright (C) 2022
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, version 3.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 */

#include "ElyByRefreshStep.h"

#include <QJsonDocument>
#include <QJsonObject>
#include <QNetworkRequest>

#include "Application.h"
#include "minecraft/auth/Parsers.h"
#include "net/NetUtils.h"
#include "net/RawHeaderProxy.h"

namespace {
static const char* kElyByAuthServer = "https://authserver.ely.by";
}

ElyByRefreshStep::ElyByRefreshStep(AccountData* data) : AuthStep(data) {}

QString ElyByRefreshStep::describe()
{
    return tr("Refreshing Ely.by session.");
}

void ElyByRefreshStep::perform()
{
    const QString accessToken = m_data->yggdrasilToken.token;
    const QString clientToken = m_data->yggdrasilToken.extra.value("clientToken").toString();

    if (accessToken.isEmpty() || clientToken.isEmpty()) {
        emit finished(AccountTaskState::STATE_DISABLED, tr("Ely.by refresh failed: missing access token or client token."));
        return;
    }

    QJsonObject requestObj;
    requestObj["accessToken"] = accessToken;
    requestObj["clientToken"] = clientToken;
    if (!m_data->minecraftProfile.id.isEmpty() && !m_data->minecraftProfile.name.isEmpty()) {
        QJsonObject profileObj;
        profileObj["id"] = m_data->minecraftProfile.id;
        profileObj["name"] = m_data->minecraftProfile.name;
        requestObj["selectedProfile"] = profileObj;
    }

    QJsonDocument doc(requestObj);
    QUrl url(QString("%1/auth/refresh").arg(kElyByAuthServer));
    auto headers = QList<Net::HeaderPair>{
        { "Content-Type", "application/json" },
        { "Accept", "application/json" },
    };
    auto [request, response] = Net::Upload::makeByteArray(url, doc.toJson(QJsonDocument::Compact));
    m_request = request;
    m_request->addHeaderProxy(std::make_unique<Net::RawHeaderProxy>(headers));
    m_request->enableAutoRetry(true);

    m_task.reset(new NetJob("ElyByRefreshStep", APPLICATION->network()));
    m_task->setAskRetry(false);
    m_task->addNetAction(m_request);

    connect(m_task.get(), &Task::finished, this, [this, response] { onRequestDone(response); });

    m_task->start();
}

void ElyByRefreshStep::onRequestDone(QByteArray* response)
{
    QString errorMessage;
    Token newToken;
    MinecraftProfile newProfile = m_data->minecraftProfile;

    if (!Parsers::parseElyByAuthResponse(*response, newToken, newProfile, errorMessage)) {
        if (m_request->error() != QNetworkReply::NoError) {
            if (Net::isApplicationError(m_request->error())) {
                emit finished(AccountTaskState::STATE_FAILED_SOFT, errorMessage.isEmpty() ? m_request->errorString() : errorMessage);
            } else {
                emit finished(AccountTaskState::STATE_OFFLINE, errorMessage.isEmpty() ? m_request->errorString() : errorMessage);
            }
            return;
        }
        emit finished(AccountTaskState::STATE_FAILED_SOFT, errorMessage.isEmpty() ? tr("Ely.by refresh failed.") : errorMessage);
        return;
    }

    m_data->yggdrasilToken = newToken;
    m_data->minecraftProfile = newProfile;
    m_data->minecraftEntitlement.ownsMinecraft = true;
    m_data->minecraftEntitlement.canPlayMinecraft = true;
    m_data->minecraftEntitlement.validity = Validity::Certain;

    emit finished(AccountTaskState::STATE_WORKING, tr("Ely.by session refreshed."));
}
