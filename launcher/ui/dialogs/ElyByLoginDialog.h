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

#pragma once

#include <QDialog>

#include "minecraft/auth/MinecraftAccount.h"
#include "net/NetJob.h"
#include "net/Upload.h"

namespace Ui {
class ElyByLoginDialog;
}

class ElyByLoginDialog : public QDialog {
    Q_OBJECT

   public:
    explicit ElyByLoginDialog(QWidget* parent = nullptr);
    ~ElyByLoginDialog() override;

    static MinecraftAccountPtr newAccount(QWidget* parent);

   private:
    void updateOkEnabled() const;
    void startLogin();
    void onRequestDone(QByteArray* response);

    Ui::ElyByLoginDialog* ui;
    MinecraftAccountPtr m_account;
    NetJob::Ptr m_task;
    Net::Upload::Ptr m_request;
};
