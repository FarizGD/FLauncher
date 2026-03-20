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

#include "ElyByLoginDialog.h"

#include <QDialogButtonBox>
#include <QJsonDocument>
#include <QJsonObject>
#include <QNetworkRequest>
#include <QPushButton>
#include <QUuid>

#include "Application.h"
#include "minecraft/auth/Parsers.h"
#include "net/NetUtils.h"
#include "net/RawHeaderProxy.h"
#include "ui_ElyByLoginDialog.h"

namespace {
static const char* kElyByAuthServer = "https://authserver.ely.by";
}

ElyByLoginDialog::ElyByLoginDialog(QWidget* parent) : QDialog(parent), ui(new Ui::ElyByLoginDialog)
{
    ui->setupUi(this);

    connect(ui->buttonBox->button(QDialogButtonBox::Ok), &QPushButton::clicked, this, &ElyByLoginDialog::startLogin);
    connect(ui->buttonBox->button(QDialogButtonBox::Cancel), &QPushButton::clicked, this, &ElyByLoginDialog::reject);

    connect(ui->usernameLine, &QLineEdit::textChanged, this, [this] { updateOkEnabled(); });
    connect(ui->passwordLine, &QLineEdit::textChanged, this, [this] { updateOkEnabled(); });

    updateOkEnabled();
}

ElyByLoginDialog::~ElyByLoginDialog()
{
    delete ui;
}

MinecraftAccountPtr ElyByLoginDialog::newAccount(QWidget* parent)
{
    ElyByLoginDialog dlg(parent);
    if (dlg.exec() == QDialog::Accepted) {
        return dlg.m_account;
    }
    return nullptr;
}

void ElyByLoginDialog::updateOkEnabled() const
{
    const bool hasUsername = !ui->usernameLine->text().trimmed().isEmpty();
    const bool hasPassword = !ui->passwordLine->text().isEmpty();
    ui->buttonBox->button(QDialogButtonBox::Ok)->setEnabled(hasUsername && hasPassword && !m_task);
}

void ElyByLoginDialog::startLogin()
{
    if (m_task) {
        return;
    }

    const QString username = ui->usernameLine->text().trimmed();
    const QString password = ui->passwordLine->text();
    const QString twoFactor = ui->twoFactorLine->text().trimmed();
    if (username.isEmpty() || password.isEmpty()) {
        return;
    }

    QString combinedPassword = password;
    if (!twoFactor.isEmpty()) {
        combinedPassword = QString("%1:%2").arg(password, twoFactor);
    }

    QJsonObject requestObj;
    QJsonObject agentObj;
    agentObj["name"] = "Minecraft";
    agentObj["version"] = 1;
    requestObj["agent"] = agentObj;
    requestObj["username"] = username;
    requestObj["password"] = combinedPassword;
    requestObj["clientToken"] = QUuid::createUuid().toString(QUuid::Id128);
    requestObj["requestUser"] = true;

    QJsonDocument doc(requestObj);
    QUrl url(QString("%1/auth/authenticate").arg(kElyByAuthServer));

    auto headers = QList<Net::HeaderPair>{
        { "Content-Type", "application/json" },
        { "Accept", "application/json" },
    };
    auto [request, response] = Net::Upload::makeByteArray(url, doc.toJson(QJsonDocument::Compact));
    m_request = request;
    m_request->addHeaderProxy(std::make_unique<Net::RawHeaderProxy>(headers));
    m_request->enableAutoRetry(true);

    m_task.reset(new NetJob("ElyByLoginDialog", APPLICATION->network()));
    m_task->setAskRetry(false);
    m_task->addNetAction(m_request);

    ui->statusLabel->setText(tr("Signing in..."));
    updateOkEnabled();

    connect(m_task.get(), &Task::finished, this, [this, response] { onRequestDone(response); });

    m_task->start();
}

void ElyByLoginDialog::onRequestDone(QByteArray* response)
{
    QString errorMessage;
    Token newToken;
    MinecraftProfile newProfile;

    if (!Parsers::parseElyByAuthResponse(*response, newToken, newProfile, errorMessage)) {
        if (m_request->error() != QNetworkReply::NoError) {
            if (Net::isApplicationError(m_request->error())) {
                ui->statusLabel->setText(errorMessage.isEmpty() ? m_request->errorString() : errorMessage);
            } else {
                ui->statusLabel->setText(tr("Failed to contact Ely.by authentication server."));
            }
        } else {
            ui->statusLabel->setText(errorMessage.isEmpty() ? tr("Ely.by login failed.") : errorMessage);
        }
        m_task.reset();
        updateOkEnabled();
        return;
    }

    m_account = MinecraftAccount::createElyBy();
    auto* accountData = m_account->accountData();
    accountData->yggdrasilToken = newToken;
    accountData->minecraftProfile = newProfile;
    accountData->minecraftEntitlement.ownsMinecraft = true;
    accountData->minecraftEntitlement.canPlayMinecraft = true;
    accountData->minecraftEntitlement.validity = Validity::Certain;
    accountData->validity_ = Validity::Certain;
    accountData->accountState = AccountState::Online;

    m_task.reset();
    accept();
}
