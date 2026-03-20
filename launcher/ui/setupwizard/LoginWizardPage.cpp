#include "LoginWizardPage.h"
#include "ui_LoginWizardPage.h"

LoginWizardPage::LoginWizardPage(QWidget* parent) : BaseWizardPage(parent), ui(new Ui::LoginWizardPage)
{
    ui->setupUi(this);
}

LoginWizardPage::~LoginWizardPage()
{
    delete ui;
}

void LoginWizardPage::initializePage() {}

bool LoginWizardPage::validatePage()
{
    return true;
}

void LoginWizardPage::retranslate()
{
    ui->retranslateUi(this);
}

void LoginWizardPage::on_pushButton_clicked()
{
    auto wiz = wizard();
    if (!wiz) {
        return;
    }
    if (wiz->currentId() == wiz->pageIds().last()) {
        wiz->accept();
    } else {
        wiz->next();
    }
}
